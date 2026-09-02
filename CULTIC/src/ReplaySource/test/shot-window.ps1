# Brings the CULTIC window to the foreground and captures only its client area.
param([string]$Out = "$env:TEMP\dsh_game.png")
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class Win {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    public struct RECT { public int Left, Top, Right, Bottom; }
}
"@
$p = Get-Process -Name "CULTIC" -ErrorAction Stop | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if ($p -eq $null) { throw "CULTIC window not found" }
$hwnd = $p.MainWindowHandle
[Win]::ShowWindow($hwnd, 9) | Out-Null   # SW_RESTORE
# Force above all normal windows without needing foreground rights.
$TOPMOST = [IntPtr](-1); $NOTOPMOST = [IntPtr](-2)
[Win]::SetWindowPos($hwnd, $TOPMOST, 0, 0, 0, 0, 0x0001 -bor 0x0002 -bor 0x0010) | Out-Null # NOSIZE|NOMOVE|SHOWWINDOW
Start-Sleep -Milliseconds 800
$r = New-Object Win+RECT
[Win]::GetWindowRect($hwnd, [ref]$r) | Out-Null
$w = $r.Right - $r.Left
$h = $r.Bottom - $r.Top
$bmp = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.Left, $r.Top, 0, 0, (New-Object System.Drawing.Size $w, $h))
$g.Dispose()
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
[Win]::SetWindowPos($hwnd, $NOTOPMOST, 0, 0, 0, 0, 0x0001 -bor 0x0002 -bor 0x0010) | Out-Null
Write-Output "$Out rect=$($r.Left),$($r.Top) ${w}x${h}"
