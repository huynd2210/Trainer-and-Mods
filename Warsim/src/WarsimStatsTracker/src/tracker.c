// Warsim Stats Tracker - loaded as a winmm.dll proxy next to Warsim.exe.
//
// Records every playthrough ("run") as one row of stats per in-game year and keeps them in
// WarsimStats\runs\ beside the game, so lifetime totals survive new games and deleted saves. After
// every change it rewrites WarsimStats\stats.html, a self-contained report with charts.
//
// The game is read, never changed. A background thread samples the stat globals four times a
// second; int3 breakpoints on MainMenu(), LoadGame(), GameOver() and TimesUp() tell a new game from
// a loaded one and mark how runs end. Game addresses are resolved at runtime from the COFF symbol
// table that ships inside Warsim.exe, so a game update only breaks the mod if it renames them.
//
// Other mods that also want to be winmm.dll can be renamed and dropped into <game>\mods\; every
// DLL there is loaded at startup.

#include <windows.h>
#include <stdint.h>
#include <stdio.h>
#include <stdarg.h>
#include <stdlib.h>
#include <string.h>
#include "winmm_names.h"
#include "report_html.h"

// ---------------------------------------------------------------- winmm forwarding

void *real_winmm[WINMM_EXPORT_COUNT]; // read by the jmp stubs in winmm_stubs.S

static void load_real_winmm(void) {
    char path[MAX_PATH + 32];
    GetSystemDirectoryA(path, MAX_PATH);
    strcat(path, "\\winmm.dll");
    HMODULE real = LoadLibraryA(path);
    for (int i = 0; i < WINMM_EXPORT_COUNT; i++)
        real_winmm[i] = real ? (void *)GetProcAddress(real, winmm_names[i]) : NULL;
}

// ---------------------------------------------------------------- paths / logging

static char g_dir[MAX_PATH];   // folder of Warsim.exe, with trailing backslash
static char g_data[MAX_PATH];  // WarsimStats folder, with trailing backslash

static void mod_log(const char *fmt, ...) {
    char path[MAX_PATH + 32];
    snprintf(path, sizeof path, "%stracker.log", g_data[0] ? g_data : g_dir);
    FILE *f = fopen(path, "a");
    if (!f) return;
    SYSTEMTIME t;
    GetLocalTime(&t);
    fprintf(f, "%04d-%02d-%02d %02d:%02d:%02d  ", t.wYear, t.wMonth, t.wDay, t.wHour, t.wMinute, t.wSecond);
    va_list ap;
    va_start(ap, fmt);
    vfprintf(f, fmt, ap);
    va_end(ap);
    fputc('\n', f);
    fclose(f);
}

static void now_stamp(char out[24]) {
    SYSTEMTIME t;
    GetLocalTime(&t);
    snprintf(out, 24, "%04d-%02d-%02d %02d:%02d", t.wYear, t.wMonth, t.wDay, t.wHour, t.wMinute);
}

// Picks <game>\WarsimStats\, or %LOCALAPPDATA%\WarsimStats\ when the game folder is read-only.
static int pick_data_dir(void) {
    char cand[2][MAX_PATH];
    snprintf(cand[0], MAX_PATH, "%sWarsimStats\\", g_dir);
    char local[MAX_PATH];
    DWORD n = GetEnvironmentVariableA("LOCALAPPDATA", local, MAX_PATH);
    snprintf(cand[1], MAX_PATH, "%s\\WarsimStats\\", n && n < MAX_PATH ? local : ".");
    for (int i = 0; i < 2; i++) {
        char runs[MAX_PATH + 8], probe[MAX_PATH + 16];
        CreateDirectoryA(cand[i], NULL);
        snprintf(runs, sizeof runs, "%sruns", cand[i]);
        CreateDirectoryA(runs, NULL);
        snprintf(probe, sizeof probe, "%swrite.test", cand[i]);
        FILE *f = fopen(probe, "w");
        if (!f) continue;
        fclose(f);
        DeleteFileA(probe);
        strcpy(g_data, cand[i]);
        return 1;
    }
    return 0;
}

// ---------------------------------------------------------------- game symbols

static unsigned char *g_base; // Warsim.exe image base (ASLR)

enum { SYM_FUNC, SYM_INT, SYM_STR };
enum { K_LEVEL, K_COUNTER, K_INCOME, K_EXPENSE };
static const char *const kind_names[] = {"level", "counter", "income", "expense"};

static struct sym { const char *name; int kind; void *addr; } syms[] = {
#define S(var, mangled, kind) {mangled, kind, NULL},
#include "symbols.h"
#undef S
};
enum {
#define S(var, mangled, kind) var,
#include "symbols.h"
#undef S
};

static struct column { const char *name, *label; int kind; volatile int *addr; } cols[] = {
#define C(var, label, kind) {#var, label, kind, NULL},
#include "columns.h"
#undef C
};
#define NCOL ((int)(sizeof cols / sizeof *cols))

static void **find_slot(const char *name) {
    for (size_t k = 0; k < sizeof syms / sizeof *syms; k++)
        if (!syms[k].addr && strcmp(name, syms[k].name) == 0) return &syms[k].addr;
    for (int k = 0; k < NCOL; k++)
        if (!cols[k].addr && strcmp(name, cols[k].name) == 0) return (void **)&cols[k].addr;
    return NULL;
}

// Resolves syms[] and cols[] from the COFF symbol table in the exe file on disk. Only the headers
// and the symbol/string tables at the end of the file are read.
static int resolve_symbols(const char *exe_path) {
    HANDLE h = CreateFileA(exe_path, GENERIC_READ, FILE_SHARE_READ, NULL, OPEN_EXISTING, 0, NULL);
    if (h == INVALID_HANDLE_VALUE) return 0;
    DWORD size = GetFileSize(h, NULL), got = 0;
    unsigned char head[4096];
    int ok = ReadFile(h, head, sizeof head, &got, NULL) && got == sizeof head;
    IMAGE_NT_HEADERS64 *nt = (void *)(head + ((IMAGE_DOS_HEADER *)head)->e_lfanew);
    DWORD symoff = ok ? nt->FileHeader.PointerToSymbolTable : 0;
    DWORD nsym = ok ? nt->FileHeader.NumberOfSymbols : 0;
    if (!symoff || symoff + nsym * 18 > size) { CloseHandle(h); return 0; }
    unsigned char *tab = HeapAlloc(GetProcessHeap(), 0, size - symoff + 1);
    SetFilePointer(h, (LONG)symoff, NULL, FILE_BEGIN);
    ok = tab && ReadFile(h, tab, size - symoff, &got, NULL) && got == size - symoff;
    CloseHandle(h);
    if (!ok) return 0;
    tab[size - symoff] = 0;

    IMAGE_SECTION_HEADER *sec = IMAGE_FIRST_SECTION(nt);
    const char *strtab = (const char *)tab + nsym * 18;
    for (DWORD i = 0; i < nsym; i++) {
        const unsigned char *e = tab + i * 18;
        char shortname[9] = {0};
        const char *name;
        if (*(const DWORD *)e == 0) {
            name = strtab + *(const DWORD *)(e + 4);
        } else {
            memcpy(shortname, e, 8);
            name = shortname;
        }
        short secno = *(const short *)(e + 12);
        if (secno > 0 && secno <= nt->FileHeader.NumberOfSections) {
            void **slot = find_slot(name);
            if (slot) *slot = g_base + sec[secno - 1].VirtualAddress + *(const DWORD *)(e + 8);
        }
        i += e[17]; // skip aux records
    }
    HeapFree(GetProcessHeap(), 0, tab);

    int missing = 0;
    for (size_t k = 0; k < sizeof syms / sizeof *syms; k++)
        if (!syms[k].addr) { mod_log("missing symbol %s", syms[k].name); missing++; }
    if (!cols[0].addr) { mod_log("missing symbol Year"); missing++; }
    for (int k = 1; k < NCOL; k++)
        if (!cols[k].addr) mod_log("missing stat %s; its column stays 0", cols[k].name);
    return missing == 0;
}

#define INT(id) (*(volatile int *)syms[id].addr)

// The game's std::string is libstdc++'s (cxx11 ABI): {char *data; size_t len; ...}. Copied with
// ReadProcessMemory so a string the game is reallocating at that moment can't crash us.
static void str_get(int id, char *out, size_t cap) {
    struct { const char *data; size_t len; } s;
    SIZE_T got = 0;
    out[0] = 0;
    if (!ReadProcessMemory(GetCurrentProcess(), syms[id].addr, &s, sizeof s, &got) || got != sizeof s) return;
    if (s.len >= cap) s.len = cap - 1;
    if (!ReadProcessMemory(GetCurrentProcess(), s.data, out, s.len, &got)) got = 0;
    out[got] = 0;
    for (char *p = out; *p; p++)
        if (*p == '\t' || *p == '\r' || *p == '\n') *p = ' '; // run files are tab-separated lines
}

// ---------------------------------------------------------------- runs

enum { ST_PLAYING, ST_FALLEN, ST_TIMESUP };
static const char *const status_names[] = {"playing", "fallen", "timesup"};

typedef struct {
    int id;
    char ruler[128], title[128], kingdom[128], fall_reason[256];
    char created[24], updated[24];
    int start_year, status, fall_year, game_overs, loads;
    double seconds;          // wall-clock time the game spent on this run
    int nrows, caprows;
    int (*rows)[NCOL];       // sorted by Year, one per in-game year (the last state seen that year)
    int dirty;
} Run;

static CRITICAL_SECTION g_lock; // guards everything below; the game thread takes it from the VEH
static Run *g_runs;
static int g_nruns, g_capruns, g_next_id = 1;
static int g_cur = -1;          // run the game is playing now, or -1 (main menu)
static int g_in_menu, g_menu_loaded, g_load_depth;
static int g_html_dirty;

static Run *cur_run(void) { return g_cur >= 0 ? &g_runs[g_cur] : NULL; }

static Run *add_run(void) {
    if (g_nruns == g_capruns) {
        g_capruns = g_capruns ? g_capruns * 2 : 16;
        g_runs = realloc(g_runs, g_capruns * sizeof *g_runs);
    }
    Run *r = &g_runs[g_nruns++];
    memset(r, 0, sizeof *r);
    return r;
}

static void sample(int row[NCOL]) {
    for (int k = 0; k < NCOL; k++) row[k] = cols[k].addr ? *cols[k].addr : 0;
}

// Sets the row for row[0]'s year and drops later years: after loading an older save, the years
// past it belong to a timeline that no longer happened.
static void put_row(Run *r, const int row[NCOL]) {
    int y = row[0];
    while (r->nrows && r->rows[r->nrows - 1][0] > y) { r->nrows--; r->dirty = 1; }
    if (r->nrows && r->rows[r->nrows - 1][0] == y) {
        if (memcmp(r->rows[r->nrows - 1], row, sizeof(int) * NCOL) == 0) return;
        memcpy(r->rows[r->nrows - 1], row, sizeof(int) * NCOL);
    } else {
        if (r->nrows == r->caprows) {
            r->caprows = r->caprows ? r->caprows * 2 : 64;
            r->rows = realloc(r->rows, r->caprows * sizeof *r->rows);
        }
        memcpy(r->rows[r->nrows++], row, sizeof(int) * NCOL);
        g_html_dirty = 1;
    }
    r->dirty = 1;
}

static void read_identity(Run *r) {
    str_get(YourName, r->ruler, sizeof r->ruler);
    str_get(YourTitle, r->title, sizeof r->title);
    str_get(YourKingdomName, r->kingdom, sizeof r->kingdom);
}

static void start_new_run(const char *why) {
    Run *r = add_run();
    r->id = g_next_id++;
    read_identity(r);
    r->start_year = INT(GameStartYear) ? INT(GameStartYear) : *cols[0].addr;
    now_stamp(r->created);
    strcpy(r->updated, r->created);
    g_cur = g_nruns - 1;
    int row[NCOL];
    sample(row);
    put_row(r, row);
    g_html_dirty = 1;
    mod_log("run %d: %s, %s of %s, year %d", r->id, why, r->ruler, r->kingdom, row[0]);
}

// A save belongs to the most recently played run with the same start year and the same ruler or
// kingdom name (either can change during a reign). No match means a save from before the tracker.
static void attach_loaded_run(void) {
    Run probe;
    read_identity(&probe);
    int start = INT(GameStartYear) ? INT(GameStartYear) : *cols[0].addr, best = -1;
    for (int i = 0; i < g_nruns; i++) {
        Run *r = &g_runs[i];
        if (r->start_year != start) continue;
        if (strcmp(r->ruler, probe.ruler) != 0 && strcmp(r->kingdom, probe.kingdom) != 0) continue;
        if (best < 0 || strcmp(r->updated, g_runs[best].updated) >= 0) best = i;
    }
    if (best < 0) {
        start_new_run("started from a save the tracker had not seen");
    } else {
        g_cur = best;
        Run *r = cur_run();
        strcpy(r->ruler, probe.ruler);
        strcpy(r->title, probe.title);
        strcpy(r->kingdom, probe.kingdom);
        mod_log("run %d: loaded at year %d", r->id, *cols[0].addr);
    }
    Run *r = cur_run();
    r->loads++;
    r->status = ST_PLAYING;
    int row[NCOL];
    sample(row);
    put_row(r, row);
    r->dirty = g_html_dirty = 1;
}

// ---------------------------------------------------------------- persistence

typedef struct { char *p; size_t len, cap; } Buf;

static void buf_add(Buf *b, const char *fmt, ...) {
    va_list ap;
    for (;;) {
        va_start(ap, fmt);
        int n = vsnprintf(b->p ? b->p + b->len : NULL, b->p ? b->cap - b->len : 0, fmt, ap);
        va_end(ap);
        if (n < 0) return;
        if (b->p && b->len + n < b->cap) { b->len += n; return; }
        b->cap = (b->cap + n + 1) * 2;
        b->p = realloc(b->p, b->cap);
    }
}

static void buf_json_str(Buf *b, const char *s) {
    buf_add(b, "\"");
    for (const unsigned char *p = (const unsigned char *)s; *p; p++) {
        if (*p == '"' || *p == '\\') buf_add(b, "\\%c", *p);
        else if (*p < 32 || *p >= 127 || *p == '<' || *p == '>' || *p == '&') buf_add(b, "\\u%04x", *p);
        else buf_add(b, "%c", *p);
    }
    buf_add(b, "\"");
}

// Write-then-rename, so a crash mid-write never leaves a half file behind.
static int write_file(const char *path, const char *data, size_t len) {
    char tmp[MAX_PATH + 8];
    snprintf(tmp, sizeof tmp, "%s.tmp", path);
    FILE *f = fopen(tmp, "wb");
    if (!f) return 0;
    int ok = fwrite(data, 1, len, f) == len;
    ok &= fclose(f) == 0;
    if (ok) ok = MoveFileExA(tmp, path, MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH);
    if (!ok) { DeleteFileA(tmp); mod_log("could not write %s (error %lu)", path, GetLastError()); }
    return ok;
}

static void run_path(const Run *r, char *out) { snprintf(out, MAX_PATH, "%sruns\\run-%04d.tsv", g_data, r->id); }

static void run_to_tsv(const Run *r, Buf *b) {
    buf_add(b, "# Warsim Stats Tracker run file v1\n");
    buf_add(b, "id\t%d\nruler\t%s\ntitle\t%s\nkingdom\t%s\nstartYear\t%d\n", r->id, r->ruler, r->title, r->kingdom, r->start_year);
    buf_add(b, "created\t%s\nupdated\t%s\nstatus\t%s\nfallYear\t%d\nfallReason\t%s\n", r->created, r->updated,
            status_names[r->status], r->fall_year, r->fall_reason);
    buf_add(b, "gameOvers\t%d\nloads\t%d\nseconds\t%.0f\ncolumns", r->game_overs, r->loads, r->seconds);
    for (int k = 0; k < NCOL; k++) buf_add(b, "\t%s", cols[k].name);
    buf_add(b, "\n");
    for (int i = 0; i < r->nrows; i++) {
        buf_add(b, "row");
        for (int k = 0; k < NCOL; k++) buf_add(b, "\t%d", r->rows[i][k]);
        buf_add(b, "\n");
    }
}

static void copy_field(char *dst, size_t cap, const char *src) {
    snprintf(dst, cap, "%s", src);
}

// Columns are matched by name, so run files written before a column was added still load.
static void load_run_file(const char *path) {
    FILE *f = fopen(path, "rb");
    if (!f) return;
    Run *r = add_run();
    int map[256], nmap = 0;
    static char line[1 << 16];
    while (fgets(line, sizeof line, f)) {
        line[strcspn(line, "\r\n")] = 0;
        char *tab = strchr(line, '\t');
        if (line[0] == '#' || !tab) continue;
        *tab = 0;
        const char *key = line, *val = tab + 1;
        if (!strcmp(key, "id")) r->id = atoi(val);
        else if (!strcmp(key, "ruler")) copy_field(r->ruler, sizeof r->ruler, val);
        else if (!strcmp(key, "title")) copy_field(r->title, sizeof r->title, val);
        else if (!strcmp(key, "kingdom")) copy_field(r->kingdom, sizeof r->kingdom, val);
        else if (!strcmp(key, "startYear")) r->start_year = atoi(val);
        else if (!strcmp(key, "created")) copy_field(r->created, sizeof r->created, val);
        else if (!strcmp(key, "updated")) copy_field(r->updated, sizeof r->updated, val);
        else if (!strcmp(key, "fallYear")) r->fall_year = atoi(val);
        else if (!strcmp(key, "fallReason")) copy_field(r->fall_reason, sizeof r->fall_reason, val);
        else if (!strcmp(key, "gameOvers")) r->game_overs = atoi(val);
        else if (!strcmp(key, "loads")) r->loads = atoi(val);
        else if (!strcmp(key, "seconds")) r->seconds = atof(val);
        else if (!strcmp(key, "status")) {
            for (int s = 0; s < 3; s++)
                if (!strcmp(val, status_names[s])) r->status = s;
        } else if (!strcmp(key, "columns")) {
            char *save = NULL;
            nmap = 0;
            for (char *t = strtok_s((char *)val, "\t", &save); t && nmap < 256; t = strtok_s(NULL, "\t", &save)) {
                map[nmap] = -1;
                for (int k = 0; k < NCOL; k++)
                    if (!strcmp(t, cols[k].name)) map[nmap] = k;
                nmap++;
            }
        } else if (!strcmp(key, "row") && nmap) {
            int row[NCOL] = {0};
            char *save = NULL;
            int i = 0;
            for (char *t = strtok_s((char *)val, "\t", &save); t && i < nmap; t = strtok_s(NULL, "\t", &save), i++)
                if (map[i] >= 0) row[map[i]] = atoi(t);
            put_row(r, row);
        }
    }
    fclose(f);
    r->dirty = 0;
    if (r->id <= 0) { g_nruns--; return; }
    if (r->id >= g_next_id) g_next_id = r->id + 1;
}

static void load_all_runs(void) {
    char pattern[MAX_PATH + 16], path[MAX_PATH * 2];
    snprintf(pattern, sizeof pattern, "%sruns\\run-*.tsv", g_data);
    WIN32_FIND_DATAA fd;
    HANDLE h = FindFirstFileA(pattern, &fd);
    if (h == INVALID_HANDLE_VALUE) return;
    do {
        snprintf(path, sizeof path, "%sruns\\%s", g_data, fd.cFileName);
        load_run_file(path);
    } while (FindNextFileA(h, &fd));
    FindClose(h);
    mod_log("loaded %d run(s)", g_nruns);
}

static void report_json(Buf *b) {
    char stamp[24];
    now_stamp(stamp);
    buf_add(b, "{\"generated\":");
    buf_json_str(b, stamp);
    buf_add(b, ",\"current\":%d,\"columns\":[", cur_run() ? cur_run()->id : 0);
    for (int k = 0; k < NCOL; k++) {
        buf_add(b, "%s{\"key\":", k ? "," : "");
        buf_json_str(b, cols[k].name);
        buf_add(b, ",\"label\":");
        buf_json_str(b, cols[k].label);
        buf_add(b, ",\"kind\":\"%s\"}", kind_names[cols[k].kind]);
    }
    buf_add(b, "],\"runs\":[");
    for (int i = 0; i < g_nruns; i++) {
        const Run *r = &g_runs[i];
        buf_add(b, "%s{\"id\":%d,\"ruler\":", i ? "," : "", r->id);
        buf_json_str(b, r->ruler);
        buf_add(b, ",\"title\":");
        buf_json_str(b, r->title);
        buf_add(b, ",\"kingdom\":");
        buf_json_str(b, r->kingdom);
        buf_add(b, ",\"created\":");
        buf_json_str(b, r->created);
        buf_add(b, ",\"updated\":");
        buf_json_str(b, r->updated);
        buf_add(b, ",\"fallReason\":");
        buf_json_str(b, r->fall_reason);
        buf_add(b, ",\"startYear\":%d,\"status\":\"%s\",\"fallYear\":%d,\"gameOvers\":%d,\"loads\":%d,\"seconds\":%.0f,\"rows\":[",
                r->start_year, status_names[r->status], r->fall_year, r->game_overs, r->loads, r->seconds);
        for (int j = 0; j < r->nrows; j++) {
            buf_add(b, "%s[", j ? "," : "");
            for (int k = 0; k < NCOL; k++) buf_add(b, "%s%d", k ? "," : "", r->rows[j][k]);
            buf_add(b, "]");
        }
        buf_add(b, "]}");
    }
    buf_add(b, "]}");
}

// Builds the outputs under the lock, writes them outside it so the game thread never waits on disk.
static void flush(int force_html) {
    enum { MAXOUT = 8 };
    char paths[MAXOUT][MAX_PATH];
    Buf bufs[MAXOUT] = {{0}};
    int n = 0;

    EnterCriticalSection(&g_lock);
    for (int i = 0; i < g_nruns && n < MAXOUT - 1; i++) {
        Run *r = &g_runs[i];
        if (!r->dirty) continue;
        r->dirty = 0;
        now_stamp(r->updated);
        run_path(r, paths[n]);
        run_to_tsv(r, &bufs[n++]);
    }
    // Mid-year changes (a battle, a purchase) refresh the report too, at most every ten seconds.
    static ULONGLONG last_html;
    ULONGLONG now = GetTickCount64();
    if (g_html_dirty || force_html || (n > 0 && now - last_html >= 10000)) {
        g_html_dirty = 0;
        last_html = now;
        snprintf(paths[n], MAX_PATH, "%sstats.html", g_data);
        const char *mark = strstr(report_html, "/*WARSIM_DATA*/null");
        buf_add(&bufs[n], "%.*s", (int)(mark - report_html), report_html);
        report_json(&bufs[n]);
        buf_add(&bufs[n], "%s", mark + strlen("/*WARSIM_DATA*/null"));
        n++;
    }
    LeaveCriticalSection(&g_lock);

    for (int i = 0; i < n; i++) {
        write_file(paths[i], bufs[i].p, bufs[i].len);
        free(bufs[i].p);
    }
}

// ---------------------------------------------------------------- game events (game thread)

enum { H_MAINMENU, H_LOADGAME, H_GAMEOVER, H_TIMESUP };

static unsigned g_fingerprint_before;

// Changes when a save is actually read; a load screen left with "back" changes nothing.
static unsigned fingerprint(void) {
    char name[128];
    str_get(YourName, name, sizeof name);
    unsigned h = 2166136261u;
    for (const char *p = name; *p; p++) h = (h ^ (unsigned char)*p) * 16777619u;
    int v[] = {*cols[0].addr, INT(GameStartYear)};
    for (int i = 0; i < 2; i++) h = (h ^ (unsigned)v[i]) * 16777619u;
    for (int k = 1; k < NCOL; k++)
        if (cols[k].addr) h = (h ^ (unsigned)*cols[k].addr) * 16777619u;
    return h;
}

static void on_entry(int hook) {
    EnterCriticalSection(&g_lock);
    Run *r = cur_run();
    switch (hook) {
    case H_MAINMENU:
        g_cur = -1;
        g_in_menu = 1;
        g_menu_loaded = 0;
        g_load_depth = 0;
        break;
    case H_LOADGAME:
        if (g_load_depth++ == 0) g_fingerprint_before = fingerprint();
        break;
    case H_GAMEOVER:
    case H_TIMESUP:
        if (!r) break;
        r->status = hook == H_GAMEOVER ? ST_FALLEN : ST_TIMESUP;
        r->fall_year = *cols[0].addr;
        if (hook == H_GAMEOVER) {
            r->game_overs++;
            str_get(GameOverReason, r->fall_reason, sizeof r->fall_reason);
        } else {
            strcpy(r->fall_reason, INT(RetirementsUp) ? "Retired" : "Reached the year limit");
        }
        r->dirty = g_html_dirty = 1;
        mod_log("run %d: %s in year %d (%s)", r->id, status_names[r->status], r->fall_year, r->fall_reason);
        break;
    }
    LeaveCriticalSection(&g_lock);
}

static void on_return(int hook) {
    EnterCriticalSection(&g_lock);
    if (hook == H_MAINMENU) {
        g_in_menu = 0;
        if (!g_menu_loaded) start_new_run("new game");
    } else if (hook == H_LOADGAME && g_load_depth > 0 && --g_load_depth == 0) {
        // LoadGame() calls itself once a save is picked, so only the outermost return counts.
        if (fingerprint() != g_fingerprint_before || !g_in_menu) {
            attach_loaded_run();
            if (g_in_menu) g_menu_loaded = 1;
        }
    }
    LeaveCriticalSection(&g_lock);
}

// ---------------------------------------------------------------- int3 hooks

// An entry breakpoint stays armed for good; on each hit it also arms a one-shot breakpoint on
// the caller's return address, which is how a function's return is seen without rewriting code.
typedef struct { unsigned char *addr; unsigned char orig; int hook, entry, pending; } Bp;
static Bp g_bps[64];
static int g_nbps;
static Bp *g_rearm; // breakpoint to put back after the single-stepped original instruction

static void poke(unsigned char *at, unsigned char v) {
    DWORD old;
    VirtualProtect(at, 1, PAGE_EXECUTE_READWRITE, &old);
    *at = v;
    VirtualProtect(at, 1, old, &old);
    FlushInstructionCache(GetCurrentProcess(), at, 1);
}

static Bp *find_bp(void *at) {
    for (int i = 0; i < g_nbps; i++)
        if (g_bps[i].addr == at) return &g_bps[i];
    return NULL;
}

static Bp *arm(unsigned char *at, int hook, int entry) {
    Bp *b = find_bp(at);
    if (!b) {
        for (int i = 0; i < g_nbps && !b; i++) // reuse a spent return breakpoint
            if (!g_bps[i].entry && g_bps[i].pending == 0 && &g_bps[i] != g_rearm) b = &g_bps[i];
        if (!b && g_nbps == (int)(sizeof g_bps / sizeof *g_bps)) return NULL;
        if (!b) b = &g_bps[g_nbps++];
        b->addr = at;
        b->orig = *at;
        b->hook = hook;
        b->entry = entry;
        b->pending = 0;
    }
    if (!entry) b->pending++;
    if (*at != 0xCC) poke(at, 0xCC);
    return b;
}

static LONG CALLBACK on_exception(EXCEPTION_POINTERS *ep) {
    DWORD code = ep->ExceptionRecord->ExceptionCode;
    CONTEXT *c = ep->ContextRecord;
    if (code == EXCEPTION_BREAKPOINT) {
        Bp *b = find_bp(ep->ExceptionRecord->ExceptionAddress);
        if (!b) return EXCEPTION_CONTINUE_SEARCH;
        c->Rip = (DWORD64)b->addr;
        poke(b->addr, b->orig);
        if (b->entry) {
            unsigned char *ret = *(unsigned char **)c->Rsp; // first instruction: [rsp] is the return address
            int hook = b->hook;
            if (hook == H_MAINMENU || hook == H_LOADGAME) arm(ret, hook, 0);
            on_entry(hook);
        } else {
            b->pending--;
            on_return(b->hook);
        }
        if (b->entry || b->pending > 0) {
            g_rearm = b;
            c->EFlags |= 0x100; // trap flag: run the original instruction, then come back
        }
        return EXCEPTION_CONTINUE_EXECUTION;
    }
    if (code == EXCEPTION_SINGLE_STEP && g_rearm) {
        poke(g_rearm->addr, 0xCC);
        g_rearm = NULL;
        c->EFlags &= ~0x100;
        return EXCEPTION_CONTINUE_EXECUTION;
    }
    return EXCEPTION_CONTINUE_SEARCH;
}

static int install_hooks(void) {
    static const struct { int sym, hook; } hooks[] = {
        {MainMenu, H_MAINMENU}, {LoadGame, H_LOADGAME}, {GameOver, H_GAMEOVER}, {TimesUp, H_TIMESUP}};
    for (size_t i = 0; i < sizeof hooks / sizeof *hooks; i++) {
        unsigned char *fn = syms[hooks[i].sym].addr;
        if (*fn != 0x55) { // push rbp: every hooked function starts with it in the build this was written for
            mod_log("unexpected first byte 0x%02x in %s", *fn, syms[hooks[i].sym].name);
            return 0;
        }
    }
    if (!AddVectoredExceptionHandler(1, on_exception)) return 0;
    for (size_t i = 0; i < sizeof hooks / sizeof *hooks; i++) arm(syms[hooks[i].sym].addr, hooks[i].hook, 1);
    return 1;
}

// ---------------------------------------------------------------- sampler thread

static DWORD WINAPI sampler(void *arg) {
    (void)arg;
    ULONGLONG last = GetTickCount64(), last_write = last;
    int last_year = 0;
    for (;;) {
        Sleep(250);
        ULONGLONG now = GetTickCount64();
        EnterCriticalSection(&g_lock);
        Run *r = cur_run();
        if (r && !g_in_menu && g_load_depth == 0) {
            int row[NCOL];
            sample(row);
            put_row(r, row);
            double before = r->seconds;
            r->seconds += (now - last) / 1000.0;
            if ((int)(r->seconds / 30) != (int)(before / 30)) r->dirty = 1; // keep play time saved
            if (row[0] != last_year) {
                last_year = row[0];
                read_identity(r); // titles change as a reign goes on
                g_html_dirty = 1;
            }
        }
        LeaveCriticalSection(&g_lock);
        last = now;
        if (now - last_write >= 2000) {
            last_write = now;
            flush(0);
        }
    }
    return 0;
}

// The report written now is the one read after the game has closed: nothing is "playing now".
static void flush_on_exit(void) {
    EnterCriticalSection(&g_lock);
    if (cur_run()) cur_run()->dirty = 1;
    g_cur = -1;
    LeaveCriticalSection(&g_lock);
    flush(1);
}

static BOOL WINAPI on_console_close(DWORD type) {
    if (type == CTRL_CLOSE_EVENT || type == CTRL_LOGOFF_EVENT || type == CTRL_SHUTDOWN_EVENT) flush_on_exit();
    return FALSE;
}

// ---------------------------------------------------------------- startup

static void load_chained_mods(void) {
    char pattern[MAX_PATH + 16], path[MAX_PATH * 2];
    snprintf(pattern, sizeof pattern, "%smods\\*.dll", g_dir);
    WIN32_FIND_DATAA fd;
    HANDLE h = FindFirstFileA(pattern, &fd);
    if (h == INVALID_HANDLE_VALUE) return;
    do {
        snprintf(path, sizeof path, "%smods\\%s", g_dir, fd.cFileName);
        mod_log("loading mods\\%s: %s", fd.cFileName, LoadLibraryA(path) ? "ok" : "FAILED");
    } while (FindNextFileA(h, &fd));
    FindClose(h);
}

static int g_enabled;

static void init_mod(void) {
    char exe[MAX_PATH];
    GetModuleFileNameA(NULL, exe, MAX_PATH);
    strcpy(g_dir, exe);
    char *slash = strrchr(g_dir, '\\');
    if (slash) slash[1] = 0;

    const char *base = strrchr(exe, '\\');
    if (_stricmp(base ? base + 1 : exe, "Warsim.exe") != 0) return; // proxy only, not our game

    InitializeCriticalSection(&g_lock);
    g_base = (unsigned char *)GetModuleHandleA(NULL);
    if (!pick_data_dir()) { mod_log("no writable folder for stats; tracker disabled"); return; }
    load_chained_mods();
    if (!resolve_symbols(exe)) { mod_log("symbols missing; tracker disabled"); return; }
    load_all_runs();
    // Hooks must be live before main() reaches MainMenu(), which it does straight away.
    if (!install_hooks()) { mod_log("hooks failed; tracker disabled"); return; }
    SetConsoleCtrlHandler(on_console_close, TRUE);
    g_html_dirty = 1;
    CloseHandle(CreateThread(NULL, 0, sampler, NULL, 0, NULL));
    g_enabled = 1;
    mod_log("enabled: %d stat columns, data in %s", NCOL, g_data);
}

BOOL WINAPI DllMain(HINSTANCE inst, DWORD reason, LPVOID reserved) {
    if (reason == DLL_PROCESS_ATTACH) {
        DisableThreadLibraryCalls(inst);
        load_real_winmm();
        init_mod();
    } else if (reason == DLL_PROCESS_DETACH && reserved && g_enabled) {
        // Process exit: the sampler thread is already gone. If it died holding the lock, skip;
        // it saved at most two seconds ago.
        if (TryEnterCriticalSection(&g_lock)) {
            LeaveCriticalSection(&g_lock);
            flush_on_exit();
        }
    }
    return TRUE;
}
