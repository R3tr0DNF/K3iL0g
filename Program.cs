using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

class Program
{
    // 1. Constantes de la API de Windows
    private const int WH_KEYBOARD_LL = 13;   // Hook de teclado de bajo nivel
    private const int WM_KEYDOWN = 0x0100;   // Mensaje: tecla presionada
    private const int WM_SYSKEYDOWN = 0x0104;
   
    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    private static IntPtr _hookID = IntPtr.Zero;
    private static LowLevelKeyboardProc _proc = HookCallback;

    private static List<KeyEvent> _historial = new List<KeyEvent>();

    private static readonly Dictionary<int, string> _teclasEspeciales = new Dictionary<int, string>
    {
        { 8,   "Backspace" },
        { 9,   "Tab" },
        { 13,  "Enter" },
        { 16,  "Shift" },
        { 17,  "Ctrl" },
        { 18,  "Alt" },
        { 19,  "Pause" },
        { 20,  "CapsLock" },
        { 27,  "Esc" },
        { 32,  "Espacio" },
        { 33,  "PageUp" },
        { 34,  "PageDown" },
        { 35,  "End" },
        { 36,  "Home" },
        { 37,  "Izquierda" },
        { 38,  "Arriba" },
        { 39,  "Derecha" },
        { 40,  "Abajo" },
        { 44,  "PrintScreen" },
        { 45,  "Insert" },
        { 46,  "Supr" },
        { 91,  "WinIzq" },
        { 92,  "WinDer" },
        { 93,  "Menu" },
        { 144, "NumLock" },
        { 145, "ScrollLock" },
        { 160, "ShiftIzq" },
        { 161, "ShiftDer" },
        { 162, "CtrlIzq" },
        { 163, "CtrlDer" },
        { 164, "AltIzq" },
        { 165, "AltDer" },
    };

    private class KeyEvent
    {
        public int Vkcode;
        public string Nombre;
        public DateTime Timestamp;

        public override string ToString()
        {
            return $"[{Timestamp:HH:mm:ss.fff}] {Nombre} (vkCode={Vkcode})";
        }
    }

    static void Main()
    {

        _hookID = SetHook(_proc);
        Console.WriteLine("Monitor activo. Presiona ESC para salir.");
        Console.WriteLine("Las teclas aparecerán aquí abajo:");
        Console.WriteLine("-----------------------------------");

        MSG msg;
        while (GetMessage(out msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        
        UnhookWindowsHookEx(_hookID);

        Console.WriteLine("Historial de captura");
        foreach (var ev in _historial)
        {
            Console.WriteLine(ev);
        }
    }


    private static IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        using (Process curProcess = Process.GetCurrentProcess())
        using (ProcessModule curModule = curProcess.MainModule)
        {
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
        }
    }


    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN ))
        {

            int vkCode = Marshal.ReadInt32(lParam);

            string nombre = TraducirTecla(vkCode);

            var ev = new KeyEvent
            {
                Vkcode = vkCode,
                Nombre = nombre,
                Timestamp = DateTime.Now
            };
            _historial.Add(ev);


            Console.WriteLine(ev);


            if (vkCode == 27)
            {
                PostQuitMessage(0);
            }
        }

        // 11. CRÍTICO: pasar el evento al siguiente hook de la cadena
        //     Si no llamas a esto, rompes el teclado del sistema
        return CallNextHookEx(_hookID, nCode, wParam, lParam);
    }

    private static string TraducirTecla(int vkCode)
    {
        if(_teclasEspeciales.TryGetValue(vkCode, out string especial))
            return especial;

        if ((vkCode >= 65 && vkCode <= 90) || (vkCode >= 48 && vkCode <= 57))
            return ((char)vkCode).ToString();
        
        if (vkCode >= 112 && vkCode <= 135)
            return $"F{vkCode - 111}";

        uint scanCode = MapVirtualKey((uint)vkCode, 0);
        if (scanCode != 0)
            return $"ScanCode_{scanCode}";

        return $"Desconocida({vkCode})";

    }


    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);


    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int nExitCode);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int pt_x;
        public int pt_y;
    }
}