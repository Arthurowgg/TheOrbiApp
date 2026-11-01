using System.Runtime.InteropServices;

namespace Orbi.Setup;

/// <summary>
/// O mínimo de Win32 para desenhar a janela do instalador sem WPF/WinForms.
/// É isso que mantém o setup em poucos MB e funcionando antes de existir .NET
/// no computador.
/// </summary>
internal static class Win32
{
    public const int WS_OVERLAPPED = 0x00000000;
    public const int WS_CAPTION = 0x00C00000;
    public const int WS_SYSMENU = 0x00080000;
    public const int WS_MINIMIZEBOX = 0x00020000;
    public const int WS_CHILD = 0x40000000;
    public const int WS_VISIBLE = 0x10000000;
    public const int WS_TABSTOP = 0x00010000;
    public const int WS_BORDER = 0x00800000;

    public const int BS_PUSHBUTTON = 0x00000000;
    public const int BS_DEFPUSHBUTTON = 0x00000001;
    public const int SS_LEFT = 0x00000000;
    public const int SS_NOPREFIX = 0x00000080;
    public const int ES_AUTOHSCROLL = 0x00000080;
    public const int ES_READONLY = 0x00000800;
    public const int ES_MULTILINE = 0x00000004;
    public const int ES_AUTOVSCROLL = 0x00000040;
    public const int WS_VSCROLL = 0x00200000;

    public const int WM_COMMAND = 0x0111;
    public const int WM_DESTROY = 0x0002;
    public const int WM_SETFONT = 0x0030;
    public const int BM_GETCHECK = 0x00F0;
    public const int WM_CLOSE = 0x0010;
    public const uint WM_PAINT = 0x000F;
    public const int EM_LINESCROLL = 0x00B6;
    public const int PBM_SETPOS = 0x0402;

    public const int SW_SHOW = 5;
    public const int SW_HIDE = 0;
    public const int COLOR_WINDOW = 5;
    public const int IDC_ARROW = 32512;

    public const int DT_LEFT = 0x00000000;
    public const int DT_WORDBREAK = 0x00000010;
    public const int DT_NOPREFIX = 0x00000800;

    public const uint MB_YESNO = 0x00000004;
    public const uint MB_ICONQUESTION = 0x00000020;
    public const uint MB_OK = 0x00000000;
    public const uint MB_ICONERROR = 0x00000010;
    public const int IDYES = 6;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CREATESTRUCT
    {
        public IntPtr lpCreateParams;
        public IntPtr hInstance;
        public IntPtr hMenu;
        public IntPtr hwndParent;
        public int cy;
        public int cx;
        public int y;
        public int x;
        public int style;
        public string? lpszName;
        public string? lpszClass;
        public int dwExStyle;
    }

    public const uint WS_EX_APPWINDOW = 0x00040000;
    public const uint CS_HREDRAW = 0x0002;
    public const uint CS_VREDRAW = 0x0001;

    public delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr CreateWindowExW(uint exStyle, string className, string windowName,
        int style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern ushort RegisterClassExW(ref WNDCLASSEX wndClass);

    [DllImport("user32.dll")]
    public static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern void ShowWindow(IntPtr hWnd, int command);

    [DllImport("user32.dll")]
    public static extern bool UpdateWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetMessageW(out MSG msg, IntPtr hWnd, uint min, uint max);

    [DllImport("user32.dll")]
    public static extern IntPtr DispatchMessageW(ref MSG msg);

    [DllImport("user32.dll")]
    public static extern void PostQuitMessage(int code);

    [DllImport("user32.dll")]
    public static extern void DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr SendMessageW(IntPtr hWnd, uint msg, IntPtr wParam, string lParam);

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern void SetWindowTextW(IntPtr hWnd, string text);

    [DllImport("user32.dll")]
    public static extern bool EnableWindow(IntPtr hWnd, bool enable);

    [DllImport("user32.dll")]
    public static extern IntPtr SetFocus(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool InvalidateRect(IntPtr hWnd, IntPtr rect, bool erase);

    [DllImport("user32.dll")]
    public static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern int ReleaseDC(IntPtr hWnd, IntPtr dc);

    [DllImport("user32.dll")]
    public static extern IntPtr SetTimer(IntPtr hWnd, IntPtr id, uint interval, IntPtr callback);

    [DllImport("user32.dll")]
    public static extern bool KillTimer(IntPtr hWnd, IntPtr id);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    [DllImport("user32.dll")]
    public static extern IntPtr GetWindowLongPtrW(IntPtr hWnd, int index);

    [DllImport("user32.dll")]
    public static extern IntPtr SetWindowLongPtrW(IntPtr hWnd, int index, IntPtr value);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    public static extern IntPtr LoadCursorW(IntPtr instance, IntPtr name);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int DrawTextW(IntPtr dc, string text, int length, ref RECT rect, int format);

    [DllImport("user32.dll")]
    public static extern IntPtr SetTextColor(IntPtr dc, int color);

    [DllImport("user32.dll")]
    public static extern IntPtr SetBkColor(IntPtr dc, int color);

    [DllImport("user32.dll")]
    public static extern IntPtr GetStockObject(int index);

    [DllImport("user32.dll")]
    public static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);

    [DllImport("user32.dll")]
    public static extern bool DeleteObject(IntPtr obj);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr CreateFontW(int height, int width, int escapement, int orientation,
        int weight, uint italic, uint underline, uint strikeOut, uint charSet, uint outputPrecision,
        uint clipPrecision, uint quality, uint pitchAndFamily, string face);

    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateSolidBrush(int color);

    [DllImport("gdi32.dll")]
    public static extern bool Rectangle(IntPtr dc, int left, int top, int right, int bottom);

    [DllImport("gdi32.dll")]
    public static extern bool FillRect(IntPtr dc, ref RECT rect, IntPtr brush);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr GetModuleHandleW(string? name);

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG
    {
        public IntPtr hWnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int x;
        public int y;
    }

    public const int FW_SEMIBOLD = 600;
    public const int FW_NORMAL = 400;
    public const uint DEFAULT_CHARSET = 1;


    public static int Rgb(int r, int g, int b) => r | (g << 8) | (b << 16);

    public static readonly int Paper = Rgb(0xF2, 0xED, 0xE3);
    public static readonly int Ink = Rgb(0x14, 0x10, 0x0C);
    public static readonly int Warm = Rgb(0x6A, 0x61, 0x52);
    public static readonly int Signal = Rgb(0xC7, 0x45, 0x2A);
    public static readonly int Film = Rgb(0x13, 0x10, 0x0C);
    public static readonly int White = Rgb(0xFF, 0xFF, 0xFF);
    public static readonly int Bone = Rgb(0xF1, 0xEB, 0xDF);
}
