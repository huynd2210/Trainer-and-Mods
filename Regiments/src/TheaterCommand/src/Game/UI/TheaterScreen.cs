using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using birdseye;
using birdseye.Fadeables;
using birdseye.Localization;
using birdseye.Regiments;
using birdseye.Regiments.GameModes;
using birdseye.Regiments.Menu.GUI;
using birdseye.Regiments.Menu.GUI.CampaignMenu;
using birdseye.Regiments.SaveStates;
using TheaterCommand.Core;
using TheaterCommand.Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TheaterCommand.Game
{
    /// <summary>
    /// The theater map: generated land on a square grid, territory tinted per side with the
    /// front line in red, towns with their points, and every formation as a NATO counter on its
    /// square. Counters are dragged to move or onto an enemy to attack; battles are markers on
    /// the map carrying the odds, fought or auto-resolved from there.
    /// </summary>
    public class TheaterScreen : MonoBehaviour
    {
        public const string RootName = "__ScreenGroup_TheaterCommand";
        private const int PaintWidth = 2048;

        private static TheaterScreen instance;
        // Painted land by campaign seed; painting takes a moment, so it happens once per session.
        private static readonly Dictionary<int, Texture2D> painted = new Dictionary<int, Texture2D>();

        private ProcOpSaveSlotRecord slot;
        private Campaign c;
        private Theater t => c.theater;

        private FadeGroup group;
        private CanvasGroup canvasGroup;
        private Canvas canvas;
        private RectTransform canvasRT;
        private RectTransform map;
        private float W;
        private float H;
        private float cell;

        private RawImage land;
        private Task<byte[]> painting;
        private int paintingSeed;
        private RawImage ownerImage;
        private Texture2D ownerTex;
        private RawImage reachImage;
        private Texture2D reachTex;
        private LineGraphic front;
        private LineGraphic enemyArrows;
        private LineGraphic pathLine;
        private RectTransform labelLayer;
        private RectTransform counterLayer;
        private RectTransform badgeLayer;
        private RectTransform oddsLayer;
        private RectTransform peekLayer;
        private RectTransform popoverLayer;
        private readonly Dictionary<int, RectTransform> counters = new Dictionary<int, RectTransform>();

        private TextMeshProUGUI opName;
        private TextMeshProUGUI desc;
        private TextMeshProUGUI hint;
        private TextMeshProUGUI logLine;
        private Button primary;
        private TextMeshProUGUI primaryText;
        private GameObject buttonTemplate;

        private Formation dragging;
        private Reach dragReach;
        private Formation hovered;
        // Unity counts double-clicks within 0.3 s only; Windows allows 0.5 s by default.
        private const float DoubleClickTime = 0.5f;
        private int lastClickId = -1;
        private float lastClickTime;
        private float hoverSince;
        // The formation whose contents are unfolded beside its counter, or -1.
        private int peekFor = -1;
        private const float PeekDelay = 0.35f;

        private IForceHooks Hooks => new GameForces(c, slot);

        // --- Entry points --------------------------------------------------------------------

        public static void Show(ProcOpSaveSlotRecord rec)
        {
            Campaign campaign = CampaignStore.Get(rec);
            if (campaign == null)
            {
                return;
            }
            if (instance == null)
            {
                instance = Build();
            }
            instance.slot = rec;
            instance.c = campaign;
            // Back from reinforcing a formation in the regiment screen.
            RosterBridge.Close(rec, campaign);
            instance.BuildMap();
            instance.Refresh();
            FadeGroupTransitionManager.active.TransitionTo(instance.group);
        }

        public static void Hide()
        {
            if (instance != null)
            {
                instance.ClosePopover();
                instance.HidePeek();
            }
        }

        public static void DestroyExisting()
        {
            GameObject old = instance != null ? instance.gameObject : GameObject.Find("ScreenGroups/" + RootName);
            if (old != null)
            {
                Destroy(old);
            }
            instance = null;
        }

        private bool IsVisible => canvasGroup != null && canvasGroup.alpha > 0.9f;

        // --- Construction --------------------------------------------------------------------

        // Clone the War Path phases screen for its canvas setup, background, header and back
        // button, and strip what is specific to it.
        private static TheaterScreen Build()
        {
            // Faded-out screen groups may be inactive, so find it through its component.
            GameObject src = Resources.FindObjectsOfTypeAll<ProcOpMenuPhasesList>().First(p => p.gameObject.scene.IsValid() && p.name == "__ScreenGroup_ProcOpPhases").gameObject;
            var holder = new GameObject("tc_holder");
            holder.SetActive(false);
            GameObject root = Instantiate(src, holder.transform);
            root.name = RootName;
            DestroyImmediate(root.GetComponent<ProcOpMenuPhasesList>());
            DestroyImmediate(root.GetComponent<FadeGroupMarker>());
            Transform cv = root.transform.Find("Canvas");
            DestroyImmediate(cv.Find("Scroll View").gameObject);
            DestroyImmediate(cv.Find("ToResultsButton").gameObject);
            foreach (L10NTextReplacer r in root.GetComponentsInChildren<L10NTextReplacer>(true))
            {
                DestroyImmediate(r);
            }
            foreach (ScreenTransitionKeyListener k in root.GetComponentsInChildren<ScreenTransitionKeyListener>(true))
            {
                k.canvasMarker = eCanvasMarkers.ProcOpSlots;
            }
            foreach (ScreenTransitionButton b in root.GetComponentsInChildren<ScreenTransitionButton>(true))
            {
                b.canvasMarker = eCanvasMarkers.ProcOpSlots;
            }

            // A game-styled button to copy for our own actions.
            GameObject accept = Resources.FindObjectsOfTypeAll<ProcOpRunSetupPanel>().First(p => p.gameObject.scene.IsValid()).acceptButton.gameObject;
            GameObject template = Instantiate(accept, holder.transform);
            template.name = "TC_ButtonTemplate";
            foreach (L10NTextReplacer r in template.GetComponentsInChildren<L10NTextReplacer>(true))
            {
                DestroyImmediate(r);
            }

            root.transform.SetParent(src.transform.parent, false);
            root.SetActive(true);
            template.transform.SetParent(root.transform, false);
            template.SetActive(false);
            Destroy(holder);

            var s = root.AddComponent<TheaterScreen>();
            s.group = root.GetComponent<FadeGroup>();
            s.canvas = cv.GetComponent<Canvas>();
            s.canvasGroup = cv.GetComponent<CanvasGroup>();
            s.canvasRT = (RectTransform)cv;
            s.buttonTemplate = template;

            Transform header = cv.Find("SlotHeader");
            s.opName = header.Find("OpName").GetComponent<TextMeshProUGUI>();
            s.desc = header.Find("SlotDesc/Desc").GetComponent<TextMeshProUGUI>();
            header.Find("SlotLbl").GetComponent<TextMeshProUGUI>().text = "";

            ProcOpSaveSlotItem anyItem = Object.FindObjectsOfType<ProcOpSaveSlotItem>(true).FirstOrDefault();
            if (anyItem != null)
            {
                Style.Red = Style.WithAlpha(anyItem.failCol, 1f);
            }

            s.hint = Style.Label(cv, "Hint", "", 22, Style.TextDim, isLight: true, align: TextAlignmentOptions.Left);
            var hr = (RectTransform)s.hint.transform;
            hr.anchorMin = new Vector2(0f, 0f);
            hr.anchorMax = new Vector2(1f, 0f);
            hr.pivot = new Vector2(0.5f, 0f);
            hr.offsetMin = new Vector2(60f, 18f);
            hr.offsetMax = new Vector2(-420f, 52f);

            s.logLine = Style.Label(cv, "Log", "", 20, Style.TextDim, align: TextAlignmentOptions.Left);
            var lr = (RectTransform)s.logLine.transform;
            lr.anchorMin = new Vector2(0f, 0f);
            lr.anchorMax = new Vector2(1f, 0f);
            lr.pivot = new Vector2(0.5f, 0f);
            lr.offsetMin = new Vector2(60f, 56f);
            lr.offsetMax = new Vector2(-420f, 96f);
            s.logLine.overflowMode = TextOverflowModes.Ellipsis;

            s.primary = s.MakeButton(cv, "Primary", "NEXT TURN", out s.primaryText);
            var er = (RectTransform)s.primary.transform;
            er.anchorMin = er.anchorMax = new Vector2(1f, 0f);
            er.pivot = new Vector2(1f, 0f);
            er.anchoredPosition = new Vector2(-60f, 16f);
            er.sizeDelta = new Vector2(320f, 86f);
            s.primary.onClick.AddListener(() => Safe(s.PrimaryAction));
            return s;
        }

        private Button MakeButton(Transform parent, string name, string text, out TextMeshProUGUI label)
        {
            GameObject go = Instantiate(buttonTemplate, parent);
            go.name = name;
            go.SetActive(true);
            Button b = go.GetComponent<Button>();
            b.onClick.RemoveAllListeners();
            label = go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.text = text;
            }
            return b;
        }

        private void BuildMap()
        {
            if (map != null)
            {
                Destroy(map.gameObject);
            }
            counters.Clear();
            Rect cr = canvasRT.rect;
            H = cr.height - 300f;
            W = H * t.aspect;
            if (W > cr.width - 120f)
            {
                W = cr.width - 120f;
                H = W / t.aspect;
            }
            cell = W / t.cols;
            map = Style.Rect(canvasRT, "TheaterMap").Place(new Vector2(0f, (120f - 180f) * 0.5f), new Vector2(W, H));
            // Keep the header, hint and button above the map.
            map.SetSiblingIndex(canvasRT.Find("SlotHeader").GetSiblingIndex());

            Image frame = Style.Image(map, "Frame", "UIElement", new Color(0f, 0f, 0f, 0.6f), sliced: true);
            ((RectTransform)frame.transform).Fill(-10f, -10f, -10f, -10f);

            land = Raw(map, "Land", null, new Color(0.16f, 0.17f, 0.15f, 1f));
            UseLand();
            ownerTex = CellTexture();
            ownerImage = Raw(map, "Owner", ownerTex, Color.white);
            reachTex = CellTexture();
            reachImage = Raw(map, "Reach", reachTex, Color.white);

            front = Lines(map, "Front", Style.Red, 4f);
            enemyArrows = Lines(map, "EnemyMoves", Style.WithAlpha(Style.Red, 0.85f), 3f);
            enemyArrows.headSize = 14f;
            labelLayer = Style.Rect(map, "Towns").Fill();
            pathLine = Lines(map, "Path", Style.Gold, 4f);
            pathLine.headSize = 16f;
            counterLayer = Style.Rect(map, "Counters").Fill();
            oddsLayer = Style.Rect(map, "Odds").Fill();
            badgeLayer = Style.Rect(map, "Battles").Fill();
            peekLayer = Style.Rect(canvasRT, "Peek").Fill();
            popoverLayer = Style.Rect(canvasRT, "Popover").Fill();
            DrawTowns();
        }

        private static RawImage Raw(Transform parent, string name, Texture tex, Color color)
        {
            RectTransform rt = Style.Rect(parent, name).Fill();
            var img = rt.gameObject.AddComponent<RawImage>();
            img.texture = tex;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private Texture2D CellTexture()
        {
            var tex = new Texture2D(t.cols, t.rows, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        private static LineGraphic Lines(Transform parent, string name, Color color, float width)
        {
            RectTransform rt = Style.Rect(parent, name).Fill();
            var g = rt.gameObject.AddComponent<LineGraphic>();
            g.color = color;
            g.width = width;
            g.raycastTarget = false;
            return g;
        }

        // The painted land, from the cache or from a background painter.
        private void UseLand()
        {
            if (painted.TryGetValue(t.seed, out Texture2D tex))
            {
                land.texture = tex;
                land.color = Color.white;
                return;
            }
            land.texture = null;
            land.color = new Color(0.16f, 0.17f, 0.15f, 1f);
            if (painting != null && paintingSeed == t.seed)
            {
                return;
            }
            // The painter only needs the land and the towns; give it its own copy.
            var snap = new Theater { seed = t.seed, cols = t.cols, rows = t.rows, aspect = t.aspect, towns = t.towns.ToList() };
            int w = PaintWidth, h = (int)(PaintWidth / t.aspect);
            paintingSeed = t.seed;
            painting = Task.Run(() => MapPainter.Paint(Core.Terrain.Of(snap), snap, w, h));
        }

        private void FinishPainting()
        {
            if (painting == null || !painting.IsCompleted)
            {
                return;
            }
            Task<byte[]> done = painting;
            painting = null;
            if (done.IsFaulted)
            {
                Mod.Log("map painting failed: " + done.Exception);
                return;
            }
            int w = PaintWidth, h = (int)(PaintWidth / t.aspect);
            // The painter writes the top row first; textures start at the bottom.
            byte[] top = done.Result;
            var flipped = new byte[top.Length];
            for (int y = 0; y < h; y++)
            {
                System.Buffer.BlockCopy(top, y * w * 4, flipped, (h - 1 - y) * w * 4, w * 4);
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            tex.SetPixelData(flipped, 0);
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 4;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply(true, true);
            painted[paintingSeed] = tex;
            if (c != null && t.seed == paintingSeed)
            {
                UseLand();
            }
        }

        // --- Geometry ------------------------------------------------------------------------

        private Vector2 Local(int cellId) => new Vector2(-W * 0.5f + (t.Cell(cellId).col + 0.5f) * cell, H * 0.5f - (t.Cell(cellId).row + 0.5f) * cell);

        private Vector2 Local(float x, float y) => new Vector2(-W * 0.5f + x / t.aspect * W, H * 0.5f - y * H);

        private int CellAt(Vector2 local)
        {
            int col = Mathf.FloorToInt((local.x + W * 0.5f) / cell);
            int row = Mathf.FloorToInt((H * 0.5f - local.y) / cell);
            Cell c0 = t.CellAt(col, row);
            return c0 != null ? c0.id : -1;
        }

        private Vector2 MapPoint(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(map, screen, canvas.worldCamera, out Vector2 local);
            return local;
        }

        // --- Drawing -------------------------------------------------------------------------

        private void Refresh()
        {
            HidePeek();
            Supply.Update(t);
            DrawOwners();
            DrawReach(null);
            DrawEnemyMoves();
            DrawCounters();
            DrawBattles();
            DrawHeader();
            Clear(oddsLayer);
            pathLine.segments.Clear();
            pathLine.SetVerticesDirty();
        }

        private void DrawOwners()
        {
            Color blue = Style.WithAlpha(Style.Blue, 0.1f);
            Color red = Style.WithAlpha(Style.Red, 0.12f);
            foreach (Cell c0 in t.cells)
            {
                ownerTex.SetPixel(c0.col, t.rows - 1 - c0.row, c0.owner == Side.Blue ? blue : red);
            }
            ownerTex.Apply(false);

            // The front: every cell edge with different owners either side.
            front.segments.Clear();
            float half = cell * 0.5f;
            foreach (Cell c0 in t.cells)
            {
                Vector2 p = Local(c0.id);
                Cell right = t.CellAt(c0.col + 1, c0.row);
                Cell down = t.CellAt(c0.col, c0.row + 1);
                if (right != null && right.owner != c0.owner)
                {
                    front.segments.Add(new LineGraphic.Segment { a = p + new Vector2(half, half + 2f), b = p + new Vector2(half, -half - 2f) });
                }
                if (down != null && down.owner != c0.owner)
                {
                    front.segments.Add(new LineGraphic.Segment { a = p + new Vector2(-half - 2f, -half), b = p + new Vector2(half + 2f, -half) });
                }
            }
            front.SetVerticesDirty();
        }

        // Where a formation can go this move, lightened; nothing when no formation is given.
        private void DrawReach(Reach reach)
        {
            var clear = new Color(0f, 0f, 0f, 0f);
            Color lit = new Color(1f, 1f, 1f, 0.24f);
            foreach (Cell c0 in t.cells)
            {
                bool on = reach != null && reach.Has(c0.id) && t.At(c0.id) == null;
                reachTex.SetPixel(c0.col, t.rows - 1 - c0.row, on ? lit : clear);
            }
            reachTex.Apply(false);
        }

        private void DrawEnemyMoves()
        {
            enemyArrows.segments.Clear();
            foreach (int[] m in t.enemyMoves)
            {
                Vector2 a = Local(m[0]), b = Local(m[1]);
                Vector2 dir = (b - a).normalized;
                enemyArrows.segments.Add(new LineGraphic.Segment { a = a + dir * cell * 0.3f, b = b - dir * cell * 0.42f, arrow = true });
            }
            enemyArrows.SetVerticesDirty();
        }

        private void DrawTowns()
        {
            Clear(labelLayer);
            foreach (Town town in t.towns)
            {
                // Points, or HQ, travel with the name in the accent colour.
                string tag = town.hq ? "  <color=#FCB341>HQ</color>" : "  <color=#FCB341>" + town.vp + "</color>";
                float size = 17f + 3f * town.size;
                TextMeshProUGUI l = Style.Label(labelLayer, town.name, town.name + tag, size, Color.white, style: FontStyles.Bold);
                l.outlineWidth = 0.22f;
                l.outlineColor = new Color32(0, 0, 0, 230);
                Vector2 at = Local(town.x, town.y) + new Vector2(0f, -cell * 0.62f);
                ((RectTransform)l.transform).Place(at, new Vector2(cell * 5f, size + 8f));
            }
        }

        private void DrawCounters()
        {
            Clear(counterLayer);
            counters.Clear();
            foreach (Formation f in t.formations.Where(f => !f.destroyed))
            {
                counters[f.id] = Counter(f);
            }
        }

        private RectTransform Counter(Formation f)
        {
            bool hostile = f.side != t.playerSide;
            RectTransform root = Style.Rect(counterLayer, "F" + f.id).Place(Local(f.cell), new Vector2(cell, cell));
            Vector2 size = hostile ? new Vector2(cell * 0.8f, cell * 0.8f) : new Vector2(cell * 0.84f, cell * 0.56f);
            RectTransform sym = Style.Rect(root, "Symbol").Place(new Vector2(0f, -cell * 0.02f), size);
            var g = sym.gameObject.AddComponent<CounterGraphic>();
            g.hostile = hostile;
            g.kind = f.kind;
            g.fill = hostile ? Style.Red : Style.Blue;
            bool selected = f == hovered || f == dragging;
            g.frame = selected ? Style.Gold : new Color(0.06f, 0.06f, 0.07f, 1f);
            g.ink = new Color(0.06f, 0.06f, 0.07f, 1f);
            g.frameWidth = selected ? 3f : 2f;
            // The player's formations that are done for this move fade back.
            bool spent = !hostile && t.phase == Phase.PlayerMoves && (f.attacked || f.refit || f.mp <= 0f);
            g.color = spent ? new Color(1f, 1f, 1f, 0.55f) : Color.white;
            g.raycastTarget = true;

            TextMeshProUGUI mark = Style.Label(root, "Size", Classify.SizeMark[f.size], 13f, Color.white, style: FontStyles.Bold);
            mark.outlineWidth = 0.25f;
            mark.outlineColor = new Color32(0, 0, 0, 230);
            ((RectTransform)mark.transform).Place(new Vector2(0f, size.y * 0.5f + 6f - cell * 0.02f), new Vector2(cell, 14f));

            float s01 = Mathf.Clamp01(f.strength01);
            float barW = cell * 0.8f;
            Image barBg = Style.Image(root, "BarBg", null, new Color(0f, 0f, 0f, 0.75f));
            ((RectTransform)barBg.transform).Place(new Vector2(0f, -cell * 0.42f), new Vector2(barW + 2f, 6f));
            Image bar = Style.Image(root, "Bar", null, s01 < 0.4f ? Style.Red : s01 < 0.7f ? Style.Gold : Style.Text);
            var br = (RectTransform)bar.transform;
            br.anchorMin = br.anchorMax = new Vector2(0.5f, 0.5f);
            br.pivot = new Vector2(0f, 0.5f);
            br.anchoredPosition = new Vector2(-barW * 0.5f, -cell * 0.42f);
            br.sizeDelta = new Vector2(barW * s01, 4f);

            // Small state marks in the corner: refitting, out of supply.
            string tag = !f.supplied ? "<color=#" + ColorUtility.ToHtmlStringRGB(Style.Red) + ">!</color>" : f.refit && !hostile ? "<color=#FCB341>R</color>" : "";
            if (tag != "")
            {
                TextMeshProUGUI tl = Style.Label(root, "Tag", tag, 16f, Color.white, style: FontStyles.Bold);
                tl.outlineWidth = 0.25f;
                tl.outlineColor = new Color32(0, 0, 0, 230);
                ((RectTransform)tl.transform).Place(new Vector2(cell * 0.38f, cell * 0.3f), new Vector2(16f, 18f));
            }

            var h = sym.gameObject.AddComponent<CounterHandle>();
            h.screen = this;
            h.formation = f.id;
            return root;
        }

        // Each battle still to decide is a small tag with our odds on the edge between the
        // defender and its attackers: gold where we attack, red where we are attacked. Decided
        // battles leave no mark; the log line says how they went.
        private void DrawBattles()
        {
            Clear(badgeLayer);
            foreach (Battle b in Rules.Pending(t))
            {
                bool ours = b.attackerSide == t.playerSide;
                float p = Combat.AttackerWinChance(t, b);
                float win = ours ? p : 1f - p;
                bool active = c.activeBattle == b.id;
                Vector2 to = Local(b.cell);
                List<Vector2> from = b.attackers.Select(t.Formation).Where(f => !f.destroyed).Select(f => Local(f.cell)).ToList();
                Vector2 at = from.Count > 0 ? Vector2.Lerp(to, from.Aggregate((x, y) => x + y) / from.Count, 0.5f) : to;
                string text = Mathf.RoundToInt(win * 100f) + "%" + (active ? "\n<size=10>IN BATTLE</size>" : "");
                RectTransform rt = Style.Rect(badgeLayer, "B" + b.id).Place(at, new Vector2(50f, active ? 36f : 22f));
                Image bg = Style.Image(rt, "Bg", "UIElement_Square", new Color(0.08f, 0.085f, 0.09f, 0.95f), sliced: true);
                ((RectTransform)bg.transform).Fill();
                bg.raycastTarget = true;
                var o = bg.gameObject.AddComponent<Outline>();
                o.effectColor = ours ? Style.Gold : Style.Red;
                o.effectDistance = new Vector2(1.5f, -1.5f);
                TextMeshProUGUI l = Style.Label(rt, "Text", text, 15f, ours ? Style.Gold : Style.Text, style: FontStyles.Bold);
                ((RectTransform)l.transform).Fill();
                var h = rt.gameObject.AddComponent<BattleHandle>();
                h.screen = this;
                h.battle = b.id;
            }
        }
        private int PendingCount => Rules.Pending(t).Count();

        private void DrawHeader()
        {
            string op = LocalizationManager.GetString(slot.runData.operationNameLocID);
            opName.text = "Theater Command  ·  " + op;
            string state;
            int pending = PendingCount;
            if (t.finished)
            {
                state = (t.winner == t.playerSide ? "<color=#FCB341>VICTORY</color>" : "<color=#" + ColorUtility.ToHtmlStringRGB(Style.Red) + ">DEFEAT</color>") + "  ·  " + t.endReason;
            }
            else if (t.phase == Phase.PlayerMoves)
            {
                state = "Your move";
            }
            else if (pending > 0)
            {
                state = (t.phase == Phase.EnemyBattles ? "The enemy attacks: " : "") + pending + (pending == 1 ? " battle" : " battles") + " to decide";
            }
            else
            {
                state = "";
            }
            int turn = Mathf.Min(t.turn + 1, t.maxTurns);
            desc.text = "Turn " + turn + " of " + t.maxTurns + "   ·   Victory points  " + t.blueVP + " : " + t.redVP + "   ·   " + state;

            string current = "T" + (t.turn + 1) + ": ";
            string prev = "T" + t.turn + ": ";
            List<string> lines = t.log.Where(l => l.StartsWith(current)).Select(l => l.Substring(current.Length)).ToList();
            if (lines.Count == 0)
            {
                lines = t.log.Where(l => l.StartsWith(prev)).Select(l => l.Substring(prev.Length)).ToList();
            }
            logLine.text = string.Join("   ·   ", lines.Skip(Mathf.Max(0, lines.Count - 4)));

            primary.gameObject.SetActive(!t.finished);
            primaryText.text = pending > 0 && t.phase != Phase.PlayerMoves ? "AUTO-RESOLVE ALL"
                : pending > 0 ? "TO BATTLE" : "NEXT TURN";
            HintFor(null);
        }

        private void HintFor(Formation f)
        {
            if (f != null)
            {
                string sup = f.supplied ? "supplied" : "<color=#" + ColorUtility.ToHtmlStringRGB(Style.Red) + ">out of supply</color>";
                string move = f.side == t.playerSide && t.phase == Phase.PlayerMoves ? "   ·   moves left " + f.mp.ToString("0.#") + "/" + f.maxMp.ToString("0") : "";
                string open = RosterBridge.CanOpen(c, f) ? "   ·   double-click: reinforce" : "";
                hint.text = f.name + "   ·   strength " + Mathf.RoundToInt(f.strength01 * 100f) + "%" + move + "   ·   dug in " + f.entrench + "/3   ·   " + sup + open;
                return;
            }
            if (t.finished)
            {
                hint.text = "Esc: back to the War Path slots";
            }
            else if (t.phase == Phase.PlayerMoves)
            {
                hint.text = "Rest on a unit to look inside   ·   drag it to move   ·   drag onto an enemy to attack   ·   right-click: refit   ·   Ctrl+Z: undo   ·   Enter: next turn";
            }
            else
            {
                hint.text = "Click a battle to fight it yourself or auto-resolve it   ·   Shift+click: auto-resolve   ·   Enter: auto-resolve all";
            }
        }

        private static void Clear(Transform layer)
        {
            for (int i = layer.childCount - 1; i >= 0; i--)
            {
                Destroy(layer.GetChild(i).gameObject);
            }
        }

        // --- Moving and attacking ------------------------------------------------------------

        private bool CanCommand(Formation f) => f != null && f.side == t.playerSide && Rules.CanAct(t, f, out _) && !f.refit;

        public void OnCounterEnter(int id)
        {
            if (dragging != null)
            {
                return;
            }
            hovered = t.formations.FirstOrDefault(f => f.id == id);
            hoverSince = Time.unscaledTime;
            HidePeek();
            HintFor(hovered);
            // Where it could go, before it is picked up.
            if (CanCommand(hovered) && hovered.mp > 0f)
            {
                DrawReach(Rules.Reachable(t, hovered));
            }
        }

        public void OnCounterExit(int id)
        {
            if (dragging == null && hovered != null && hovered.id == id)
            {
                hovered = null;
                HidePeek();
                HintFor(null);
                DrawReach(null);
            }
        }

        public void OnCounterBeginDrag(int id)
        {
            Formation f = t.Formation(id);
            if (!CanCommand(f))
            {
                return;
            }
            ClosePopover();
            HidePeek();
            dragging = f;
            dragReach = Rules.Reachable(t, f);
            DrawReach(dragReach);
            // Odds on every enemy this formation can attack, with whoever already attacks it.
            Clear(oddsLayer);
            foreach (Formation e in t.Alive(Theater.Other(f.side)))
            {
                if (!Rules.CanAttack(t, f, e.cell, dragReach, out _))
                {
                    continue;
                }
                float p = Combat.AttackerWinChance(t, AttackGroup(f, e.cell), e.cell);
                RectTransform rt = Style.Rect(oddsLayer, "O" + e.id).Place(Local(e.cell) + new Vector2(0f, cell * 0.52f), new Vector2(56f, 24f));
                Image bg = Style.Image(rt, "Bg", "UIElement_Square", new Color(0.08f, 0.085f, 0.09f, 0.95f), sliced: true);
                ((RectTransform)bg.transform).Fill();
                TextMeshProUGUI l = Style.Label(rt, "P", Mathf.RoundToInt(p * 100f) + "%", 16f, Style.Gold, style: FontStyles.Bold);
                ((RectTransform)l.transform).Fill();
            }
            counters[f.id].SetAsLastSibling();
        }

        private List<Formation> AttackGroup(Formation f, int target)
        {
            Battle b = t.BattleAt(target);
            var group = b == null ? new List<Formation>() : b.attackers.Select(t.Formation).ToList();
            group.Add(f);
            return group;
        }

        public void OnCounterDrag(int id, Vector2 screen)
        {
            if (dragging == null)
            {
                return;
            }
            Vector2 p = MapPoint(screen);
            counters[dragging.id].anchoredPosition = p;
            int target = CellAt(p);
            pathLine.segments.Clear();
            hint.text = DragPreview(dragging, target);
            if (target >= 0)
            {
                List<int> path = null;
                int attackTarget = -1;
                if (t.EnemyAt(target, dragging.side) && Rules.CanAttack(t, dragging, target, dragReach, out _))
                {
                    path = dragReach.PathTo(Rules.BestAttackCell(t, dragging, target, dragReach));
                    attackTarget = target;
                }
                else if (Rules.CanMove(t, dragging, target, dragReach, out _))
                {
                    path = dragReach.PathTo(target);
                }
                if (path != null)
                {
                    for (int i = 1; i < path.Count; i++)
                    {
                        pathLine.segments.Add(new LineGraphic.Segment { a = Local(path[i - 1]), b = Local(path[i]), arrow = attackTarget < 0 && i == path.Count - 1 });
                    }
                    if (attackTarget >= 0)
                    {
                        Vector2 a = Local(path[path.Count - 1]), b = Local(attackTarget);
                        pathLine.segments.Add(new LineGraphic.Segment { a = a, b = Vector2.Lerp(a, b, 0.7f), arrow = true });
                    }
                }
            }
            pathLine.SetVerticesDirty();
        }

        // What dropping here would do, with the odds for an attack, before the player commits.
        private string DragPreview(Formation f, int target)
        {
            if (target < 0)
            {
                return "Drop on the map";
            }
            string place = t.NearestTown(target).name;
            if (t.EnemyAt(target, f.side))
            {
                if (!Rules.CanAttack(t, f, target, dragReach, out string no))
                {
                    return "Cannot attack: " + no;
                }
                List<Formation> group = AttackGroup(f, target);
                float p = Combat.AttackerWinChance(t, group, target);
                string with = group.Count > 1 ? " with " + (group.Count - 1) + " more" : "";
                return "Attack " + t.At(target).name + " near " + place + with + ":  <color=#FCB341>" + Mathf.RoundToInt(p * 100f) + "% to win</color>";
            }
            if (target == f.cell)
            {
                return f.name + ": stay";
            }
            if (!Rules.CanMove(t, f, target, dragReach, out string why))
            {
                return "Cannot move there: " + why;
            }
            string terrain = t.Cell(target).terrain == TerrainKind.Forest ? "forest" : t.Cell(target).terrain == TerrainKind.Urban ? "town" : "open ground";
            return (t.Cell(target).owner != f.side ? "Take ground near " : "Move near ") + place + "   ·   " + terrain
                + "   ·   moves left after: " + (f.mp - dragReach.cost[target]).ToString("0.#");
        }

        public void OnCounterEndDrag(int id, Vector2 screen)
        {
            if (dragging == null)
            {
                return;
            }
            Formation f = dragging;
            Reach reach = dragReach;
            dragging = null;
            dragReach = null;
            int target = CellAt(MapPoint(screen));
            if (target >= 0 && t.EnemyAt(target, f.side))
            {
                if (Rules.CanAttack(t, f, target, reach, out _))
                {
                    Rules.DeclareAttack(t, f, target, -1, reach);
                }
            }
            else if (target >= 0 && Rules.CanMove(t, f, target, reach, out _))
            {
                Rules.Move(t, f, target, reach);
            }
            Save();
            Refresh();
        }

        public void OnCounterClick(int id, PointerEventData.InputButton button, int clicks)
        {
            Formation f = t.Formation(id);
            // Double-click: into the formation's regiment screen, to reinforce it.
            if (button == PointerEventData.InputButton.Left)
            {
                bool twice = clicks >= 2 || (lastClickId == id && Time.unscaledTime - lastClickTime <= DoubleClickTime);
                lastClickId = twice ? -1 : id;
                lastClickTime = Time.unscaledTime;
                if (!twice)
                {
                    return;
                }
                string why = RosterBridge.WhyNot(c, f);
                if (why != null)
                {
                    hint.text = why;
                    return;
                }
                HidePeek();
                RosterBridge.Open(slot, c, f);
                return;
            }
            if (button != PointerEventData.InputButton.Right || f.side != t.playerSide || !Rules.CanAct(t, f, out _) || f.moved)
            {
                return;
            }
            Rules.SetRefit(t, f, !f.refit);
            Save();
            Refresh();
            HintFor(f);
        }

        private void Undo()
        {
            if (!Rules.CanUndo(t))
            {
                return;
            }
            Rules.Undo(t);
            Save();
            Refresh();
        }

        private void PrimaryAction()
        {
            ClosePopover();
            if (t.finished)
            {
                return;
            }
            if (t.phase != Phase.PlayerMoves)
            {
                foreach (Battle b in Rules.Pending(t).Where(b => b.id != c.activeBattle).ToList())
                {
                    Rules.AutoResolve(t, b, Hooks);
                }
            }
            Continue();
        }

        // Step the turn on as far as it goes without the player.
        private void Continue()
        {
            if (!t.finished && (t.phase == Phase.PlayerMoves || PendingCount == 0))
            {
                Rules.Advance(t, Hooks);
            }
            Save();
            Refresh();
        }

        private void Save() => CampaignStore.Save(slot.saveSlot, c);

        // --- Battles -------------------------------------------------------------------------

        public void OnBattleClick(int id, RectTransform badge)
        {
            Battle b = t.battles.First(x => x.id == id);
            if (t.phase == Phase.PlayerMoves)
            {
                // Still giving orders: the attack is only planned (Ctrl+Z takes it back).
                return;
            }
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                AutoResolve(b);
                return;
            }
            OpenPopover(b, badge);
        }

        private void AutoResolve(Battle b)
        {
            ClosePopover();
            if (c.activeBattle == b.id)
            {
                c.activeBattle = -1;
                c.activeFormation = -1;
                c.launchPhase = -1;
            }
            Rules.AutoResolve(t, b, Hooks);
            if (PendingCount == 0)
            {
                Continue();
                return;
            }
            Save();
            Refresh();
        }

        private void OpenPopover(Battle b, RectTransform badge)
        {
            ClosePopover();
            // A click anywhere outside the popover closes it.
            Image blocker = Style.Image(popoverLayer, "Blocker", null, new Color(0f, 0f, 0f, 0.001f));
            ((RectTransform)blocker.transform).Fill();
            blocker.raycastTarget = true;
            blocker.gameObject.AddComponent<Button>().onClick.AddListener(() => Safe(ClosePopover));

            Vector2 local = popoverLayer.InverseTransformPoint(badge.position);
            float y = local.y > 0f ? -150f : 150f;
            RectTransform panel = Style.Rect(popoverLayer, "Panel").Place(local + new Vector2(0f, y), new Vector2(560f, 230f));
            Image bg = Style.Image(panel, "Bg", "UIElement", Style.Panel, sliced: true);
            ((RectTransform)bg.transform).Fill();
            bg.raycastTarget = true;

            Formation cmd = BattleBridge.Commander(t, b);
            float p = Combat.AttackerWinChance(t, b);
            bool ours = b.attackerSide == t.playerSide;
            float win = ours ? p : 1f - p;
            TextMeshProUGUI title = Style.Label(panel, "Title", (ours ? "Attack" : "Defence") + " at " + t.NearestTown(b.cell).name, 30, Style.Gold, isLight: true, style: FontStyles.SmallCaps);
            ((RectTransform)title.transform).Place(new Vector2(0f, 80f), new Vector2(530f, 40f));
            string who = cmd != null ? "Commanded by " + cmd.name : "";
            TextMeshProUGUI sub = Style.Label(panel, "Sub", who + "\nAuto-resolve odds: " + Mathf.RoundToInt(win * 100f) + "% to win", 20, Style.Text);
            sub.enableWordWrapping = true;
            ((RectTransform)sub.transform).Place(new Vector2(0f, 22f), new Vector2(530f, 60f));

            bool canFight = cmd != null && c.Force(cmd.id) != null;
            Button fight = MakeButton(panel, "Fight", c.activeBattle == b.id ? "RETURN TO BATTLE" : "FIGHT", out _);
            ((RectTransform)fight.transform).Place(new Vector2(-135f, -62f), ((RectTransform)fight.transform).sizeDelta);
            fight.interactable = canFight;
            fight.onClick.AddListener(() => Safe(() => Fight(b)));
            Button auto = MakeButton(panel, "Auto", "AUTO-RESOLVE", out _);
            ((RectTransform)auto.transform).Place(new Vector2(135f, -62f), ((RectTransform)auto.transform).sizeDelta);
            auto.onClick.AddListener(() => Safe(() => AutoResolve(b)));
        }

        private void Fight(Battle b)
        {
            ClosePopover();
            if (c.activeBattle == b.id && c.launchPhase >= 0)
            {
                BattleBridge.OpenRegimentScreen(slot, c.launchPhase);
                return;
            }
            BattleBridge.Launch(slot, c, b);
        }

        // --- Looking inside a formation ------------------------------------------------------

        // Sides of the counter the card may unfold to, the rear first.
        private static readonly Vector2[] PeekSides = { Vector2.left, Vector2.right, Vector2.up, Vector2.down };

        // Resting the pointer on a counter unfolds its contents beside it, inside the map, on
        // whichever side hides the fewest squares it could move to and the fewest other counters.
        private void ShowPeek(Formation f)
        {
            HidePeek();
            peekFor = f.id;
            RectTransform card = UnitPeek.Build(peekLayer, t, f, c, slot);
            if (card == null)
            {
                return;
            }
            Vector2 mapAt = peekLayer.InverseTransformPoint(map.position);
            Vector2 at = mapAt + Local(f.cell);
            Vector2 half = card.sizeDelta * 0.5f;
            var area = new Rect(mapAt.x - W * 0.5f, mapAt.y - H * 0.5f, W, H);
            float gap = cell * 0.75f;
            Reach reach = CanCommand(f) && f.mp > 0f ? Rules.Reachable(t, f) : null;
            List<Vector2> others = t.formations.Where(o => !o.destroyed && o != f).Select(o => mapAt + Local(o.cell)).ToList();

            Town hq = t.HQ(f.side);
            float rear = hq == null || Local(hq.cell).x <= Local(f.cell).x ? -1f : 1f;
            Vector2 best = at;
            float bestScore = float.MaxValue;
            foreach (Vector2 side in PeekSides)
            {
                Vector2 dir = side.y == 0f ? new Vector2(side.x * rear, 0f) : side;
                Vector2 p = at + new Vector2(dir.x * (gap + half.x), dir.y * (gap + half.y));
                p.x = Mathf.Clamp(p.x, area.xMin + half.x, area.xMax - half.x);
                p.y = Mathf.Clamp(p.y, area.yMin + half.y, area.yMax - half.y);
                var r = new Rect(p - half, card.sizeDelta);
                if (r.Overlaps(new Rect(at - Vector2.one * cell * 0.5f, Vector2.one * cell)))
                {
                    continue;
                }
                float score = others.Count(r.Contains) * 2f + (reach == null ? 0 : reach.cost.Keys.Count(id => r.Contains(mapAt + Local(id)))) * 3f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = p;
                }
            }
            card.anchoredPosition = best;
        }

        private void HidePeek()
        {
            peekFor = -1;
            if (peekLayer != null)
            {
                Clear(peekLayer);
            }
        }

        private void ClosePopover()
        {
            if (popoverLayer != null)
            {
                Clear(popoverLayer);
            }
        }

        // --- Development: drive the real input handlers -------------------------------------

        public static TheaterScreen Instance => instance;

        public Campaign Campaign => c;

        public string DevDrag(int formation, int cellId)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, map.TransformPoint(Local(cellId)));
            OnCounterBeginDrag(formation);
            OnCounterDrag(formation, screen);
            string preview = hint.text;
            OnCounterEndDrag(formation, screen);
            return preview;
        }

        public void DevPrimary() => PrimaryAction();

        public void DevBattle(int battle, bool fight)
        {
            Battle b = t.battles.First(x => x.id == battle);
            if (fight)
            {
                Fight(b);
            }
            else
            {
                AutoResolve(b);
            }
        }

        public void DevHover(int formation) => OnCounterEnter(formation);

        // What a real click at the formation's square hits, then two single clicks on it the
        // given seconds apart, as the input module delivers them.
        public void DevDoubleClick(int formation, float gap, System.Action<string> log)
        {
            Vector2 at = DevScreenPoint(formation);
            var ped = new PointerEventData(EventSystem.current) { position = at, button = PointerEventData.InputButton.Left, clickCount = 1 };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(ped, hits);
            GameObject top = hits.Count > 0 ? hits[0].gameObject : null;
            GameObject target = top != null ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(top) : null;
            log("a click there hits " + (top != null ? top.name + " in " + top.transform.parent.name : "nothing") + ", handled by " + (target != null ? target.name : "nobody"));
            if (target != null)
            {
                StartCoroutine(TwoClicks(target, ped, gap));
            }
        }

        private System.Collections.IEnumerator TwoClicks(GameObject target, PointerEventData ped, float gap)
        {
            ExecuteEvents.Execute(target, ped, ExecuteEvents.pointerClickHandler);
            yield return new WaitForSecondsRealtime(gap);
            ExecuteEvents.Execute(target, ped, ExecuteEvents.pointerClickHandler);
        }

        public Vector2 DevScreenPoint(int formation) => RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, map.TransformPoint(Local(t.Formation(formation).cell)));

        public void DevBeginDrag(int formation, int cellId)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, map.TransformPoint(Local(cellId)));
            OnCounterBeginDrag(formation);
            OnCounterDrag(formation, screen);
        }

        // The game quits on any unhandled exception, so nothing of ours may throw into Unity.
        public static void Safe(System.Action a)
        {
            try
            {
                a();
            }
            catch (System.Exception e)
            {
                Mod.Log("Theater Command error: " + e);
            }
        }

        private void Update() => Safe(Tick);

        private void Tick()
        {
            FinishPainting();
            if (!IsVisible || c == null)
            {
                return;
            }
            if (hovered != null && dragging == null && peekFor != hovered.id && !hovered.destroyed && Time.unscaledTime - hoverSince >= PeekDelay)
            {
                ShowPeek(hovered);
            }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                PrimaryAction();
            }
            if (Input.GetKeyDown(KeyCode.Z) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
            {
                Undo();
            }
        }
    }

    public class CounterHandle : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        public TheaterScreen screen;
        public int formation;

        public void OnPointerEnter(PointerEventData e) => TheaterScreen.Safe(() => screen.OnCounterEnter(formation));
        public void OnPointerExit(PointerEventData e) => TheaterScreen.Safe(() => screen.OnCounterExit(formation));
        public void OnBeginDrag(PointerEventData e) => TheaterScreen.Safe(() => screen.OnCounterBeginDrag(formation));
        public void OnDrag(PointerEventData e) => TheaterScreen.Safe(() => screen.OnCounterDrag(formation, e.position));
        public void OnEndDrag(PointerEventData e) => TheaterScreen.Safe(() => screen.OnCounterEndDrag(formation, e.position));
        public void OnPointerClick(PointerEventData e) => TheaterScreen.Safe(() => screen.OnCounterClick(formation, e.button, e.clickCount));
    }

    public class BattleHandle : MonoBehaviour, IPointerClickHandler
    {
        public TheaterScreen screen;
        public int battle;

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left)
            {
                TheaterScreen.Safe(() => screen.OnBattleClick(battle, (RectTransform)transform));
            }
        }
    }
}
