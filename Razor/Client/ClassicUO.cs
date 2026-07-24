using Assistant.UI;
using CUO_API;
using RazorEnhanced;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;


namespace Assistant
{
    public partial class Engine
    {

        public static unsafe void Install(PluginHeader* plugin)
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
            {
                string[] fields = e.Name.Split(',');
                string name = fields[0];
                string culture = fields[2];

                if (name.EndsWith(".resources") && !culture.EndsWith("neutral"))
                {
                    return null;
                }

                AssemblyName askedassembly = new(e.Name);

                bool isdll = File.Exists(Path.Combine(RootPath, askedassembly.Name + ".dll"));

                return Assembly.LoadFile(Path.Combine(RootPath, askedassembly.Name + (isdll ? ".dll" : ".exe")));
            };


            //ClassicUO.Configuration.Settings settings = ClassicUO.Configuration.Settings.Get();
            Install2(plugin);
        }

        public static unsafe void Install2(PluginHeader* plugin)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            //SplashScreen.Start();
            m_ActiveWnd = SplashScreen.Instance;

            ClassicUOClient.UOFilePath =
                ((OnGetUOFilePath)Marshal.GetDelegateForFunctionPointer(plugin->GetUOFilePath, typeof(OnGetUOFilePath))
                )();

            ClassicUOClient cuo = new();
            Client.Instance = cuo;

            if (cuo.InitPlugin(plugin))
            {
                cuo.RunUI();
            }
        }

    }
    public class ClassicUOClient : Client
    {
        private const string ExpectedCuoSuoxFingerprint = "c82a3dfc4977570a99885c4cc6d8ebc1d128922c2b30a6f2543bba9afe706414";
        private const byte ExpectedCuoSuoxVersion = 0x01;
        private const ushort ExpectedCuoSuoxBuildId = 1;
        private const int ExpectedCuoBindingMagic = unchecked((int)0xc82a3dfc);
        private static bool _cuoBindingRejected;

        public static string UOFilePath { get; set; }
        public override Process ClientProcess => m_ClientProcess;
        public override bool ClientRunning => m_ClientRunning;
        private uint m_In, m_Out;

        private readonly Process m_ClientProcess = null;
        private bool m_ClientRunning = false;
        private Version m_ClientVersion;

        private static OnPacketSendRecv _sendToClient, _sendToServer, _recv, _send;
        private static OnGetPacketLength _getPacketLength;
        private static OnGetPlayerPosition _getPlayerPosition;
        private static OnCastSpell _castSpell;
        private static OnGetStaticImage _getStaticImage;
        private static OnTick _tick;
        private static RequestMove _requestMove;
        private static OnSetTitle _setTitle;
        private static OnGetUOFilePath _uoFilePath;


        private static OnHotkey _onHotkeyPressed;
        private static OnMouse _onMouse;
        private static OnUpdatePlayerPosition _onUpdatePlayerPosition;
        private static OnClientClose _onClientClose;
        private static OnInitialize _onInitialize;
        private static OnConnected _onConnected;
        private static OnDisconnected _onDisconnected;
        private static OnFocusGained _onFocusGained;
        private static OnFocusLost _onFocusLost;
        private IntPtr m_ClientWindow;
        private static bool m_Ready = false;

        private delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr window, uint command);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr window, out WindowRect rectangle);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr window, StringBuilder className, int maxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr window, StringBuilder title, int maxCount);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong(IntPtr window, int index);

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowRect
        {
            internal int Left;
            internal int Top;
            internal int Right;
            internal int Bottom;
        }

        static ClassicUOClient()
        {
            Client.IsOSI = false;
            string server = RazorEnhanced.CUO.GetSetting("IP");
            IPAddress address = Dns.GetHostAddresses(server)[0];
            m_LastConnection = address;
        }

        internal override bool Init(RazorEnhanced.Shard selected)
        {

            base.Init(selected);

            // Spin up CUO
            Process cuo = new();
            cuo.StartInfo.FileName = selected.CUOClient;
            cuo.StartInfo.WorkingDirectory = Path.GetDirectoryName(selected.CUOClient);
            int osiEnc = 0;
            if (selected.OSIEnc)
            {
                osiEnc = 5;
            }
            if (File.Exists(selected.ClientPath))
            {
                var clientVersion = FileVersionInfo.GetVersionInfo(selected.ClientPath);
                string verString = String.Format("{0:00}.{1:0}.{2:0}.{3:D1}", clientVersion.FileMajorPart, clientVersion.FileMinorPart, clientVersion.FileBuildPart, clientVersion.FilePrivatePart);
                cuo.StartInfo.Arguments = String.Format("-ip {0} -port {1} -uopath \"{2}\" -no_server_ping -encryption {3} -plugins \"{4}\" -clientversion \"{5}\"",
                                            selected.Host, selected.Port, ShortFileName(selected.ClientFolder), osiEnc,
                                            ShortFileName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                                            verString);
            }
            else
            {
                cuo.StartInfo.Arguments = String.Format("-ip {0} -port {1} -uopath \"{2}\" -no_server_ping -encryption {3} -plugins \"{4}\"",
                                            selected.Host, selected.Port, ShortFileName(selected.ClientFolder), osiEnc,
                                            ShortFileName(System.Reflection.Assembly.GetExecutingAssembly().Location)
                                            );
            }
            cuo.Start();
            m_Running = false;
            return false;

        }


        public override void SetMapWndHandle(Form mapWnd)
        {
        }

        public override void RequestStatbarPatch(bool preAOS)
        {
        }

        public override void SetCustomNotoHue(int hue)
        {
        }

        public override void SetSmartCPU(bool enabled)
        {
        }

        public override void SetGameSize(int x, int y)
        {
        }

        public override Loader_Error LaunchClient(string client)
        {
            return Loader_Error.SUCCESS;
        }

        public override bool ClientEncrypted { get; set; }

        public override bool ServerEncrypted { get; set; }

        public static Assembly CUOAssembly { get { return System.Reflection.Assembly.GetEntryAssembly(); } }
        public static Queue<Action> CUOActionQueue { get; set; } = new Queue<Action>();

        public unsafe bool InitPlugin(PluginHeader* header)
        {
            _sendToClient =
                (OnPacketSendRecv)Marshal.GetDelegateForFunctionPointer(header->Recv, typeof(OnPacketSendRecv));
            _sendToServer =
                (OnPacketSendRecv)Marshal.GetDelegateForFunctionPointer(header->Send, typeof(OnPacketSendRecv));
            _getPacketLength =
                (OnGetPacketLength)Marshal.GetDelegateForFunctionPointer(header->GetPacketLength,
                    typeof(OnGetPacketLength));
            _getPlayerPosition =
                (OnGetPlayerPosition)Marshal.GetDelegateForFunctionPointer(header->GetPlayerPosition,
                    typeof(OnGetPlayerPosition));
            _castSpell = (OnCastSpell)Marshal.GetDelegateForFunctionPointer(header->CastSpell, typeof(OnCastSpell));
            _getStaticImage =
                (OnGetStaticImage)Marshal.GetDelegateForFunctionPointer(header->GetStaticImage,
                    typeof(OnGetStaticImage));
            _requestMove =
                (RequestMove)Marshal.GetDelegateForFunctionPointer(header->RequestMove, typeof(RequestMove));
            _setTitle = (OnSetTitle)Marshal.GetDelegateForFunctionPointer(header->SetTitle, typeof(OnSetTitle));
            _uoFilePath =
                (OnGetUOFilePath)Marshal.GetDelegateForFunctionPointer(header->GetUOFilePath, typeof(OnGetUOFilePath));
            m_ClientVersion = new Version((byte)(header->ClientVersion >> 24), (byte)(header->ClientVersion >> 16),
                (byte)(header->ClientVersion >> 8), (byte)header->ClientVersion);
            m_ClientRunning = true;
            m_ClientWindow = header->HWND;

            if (!VerifyCuoBinding(false, out string failure))
            {
                RejectCuoBinding(failure);
                return false;
            }

            _tick = Tick;
            _recv = OnRecv;
            _send = OnSend;
            _onHotkeyPressed = OnHotKeyHandler;
            _onMouse = OnMouseHandler;
            _onUpdatePlayerPosition = OnPlayerPositionChanged;
            _onClientClose = OnClientClosing;
            _onInitialize = OnInitialize;
            _onConnected = OnConnected;
            _onDisconnected = OnDisconnected;
            _onFocusGained = OnFocusGained;
            _onFocusLost = OnFocusLost;
            header->Tick = Marshal.GetFunctionPointerForDelegate(_tick);
            header->OnRecv = Marshal.GetFunctionPointerForDelegate(_recv);
            header->OnSend = Marshal.GetFunctionPointerForDelegate(_send);
            header->OnHotkeyPressed = Marshal.GetFunctionPointerForDelegate(_onHotkeyPressed);
            header->OnMouse = Marshal.GetFunctionPointerForDelegate(_onMouse);
            header->OnPlayerPositionChanged = Marshal.GetFunctionPointerForDelegate(_onUpdatePlayerPosition);
            header->OnClientClosing = Marshal.GetFunctionPointerForDelegate(_onClientClose);
            header->OnInitialize = Marshal.GetFunctionPointerForDelegate(_onInitialize);
            header->OnConnected = Marshal.GetFunctionPointerForDelegate(_onConnected);
            header->OnDisconnected = Marshal.GetFunctionPointerForDelegate(_onDisconnected);
            header->OnFocusGained = Marshal.GetFunctionPointerForDelegate(_onFocusGained);
            header->OnFocusLost = Marshal.GetFunctionPointerForDelegate(_onFocusLost);

            RazorEnhanced.Shard fake_shard =
                new("Classic UO Default", Path.Combine(ClassicUOClient.UOFilePath, "client.exe"),
                                        ClassicUOClient.UOFilePath, "", "127.0.0.1", 1000, true, false, true);
            base.Init(fake_shard);
            RazorEnhanced.Settings.Load(RazorEnhanced.Profiles.LastUsed());
            Start(fake_shard);
            m_Ready = true;
            return true;
        }

        public unsafe override bool InstallHooks(IntPtr pluginPtr)
        {
            //Engine.MainWindow.SafeAction((s) => { Engine.MainWindow.MainForm_EndLoad(); });
            return true;
        }

        private void Tick()
        {
            Timer.Slice();

            while (CUOActionQueue.Count > 0)
            {
                Action action = CUOActionQueue.Dequeue();
                action?.Invoke();
            }
        }

        private void OnPlayerPositionChanged(int x, int y, int z)
        {
            if (World.Player != null)
            {
                World.Player.Position = new Point3D(x, y, z);
                World.Player.WalkScriptRequest = 2;
            }
        }

        internal static void RunTheUI()
        {
            AutoDocIO.UpdateDocs();
            Engine.MainWnd = new MainForm();
            if (!IsOSI)
            {
                Engine.MainWindow.SafeAction(s => { s.DisableRecorder(); });
                Engine.MainWindow.SafeAction(s => { s.DisableSmartCpu(); });
                Engine.MainWindow.SafeAction(s => { s.DisableGameSize(); });
            }
            Application.Run(Engine.MainWnd);
        }
        public override void RunUI()
        {
            Thread t = new(() => { RunTheUI(); });
            t.SetApartmentState(ApartmentState.STA);
            t.IsBackground = true;
            t.Start();
        }
        internal override void SelectedShard(RazorEnhanced.Shard shard)
        {
            return;
        }

        private unsafe bool OnRecv(ref byte[] data, ref int length)
        {
            bool result = true;
            try
            {
                m_In += (uint)length;
                fixed (byte* ptr = data)
                {
                    byte id = data[0];

                    PacketReader reader = null;
                    Packet packet = null;
                    bool isView = PacketHandler.HasServerViewer(id);
                    bool isFilter = PacketHandler.HasServerFilter(id);


                    if (isView)
                    {

                        reader = new PacketReader(ptr, length, PacketsTable.IsDynLength(id));
                        result = !PacketHandler.OnServerPacket(id, reader, packet);
                    }
                    else if (isFilter)
                    {
                        packet = new Packet(data, length, PacketsTable.IsDynLength(id));
                        result = !PacketHandler.OnServerPacket(id, reader, packet);
                    }
                    //TODO: check if this is done correctly
                    PacketLogger.SharedInstance.LogPacketData(PacketPath.ServerToClient, data, result);
                }
            }
            catch (Exception e)
            {
                Utility.Logger.Debug("{0} crash in onRecv {1}", System.Reflection.MethodBase.GetCurrentMethod().Name, e.ToString());
            }
            return result;
        }

        private unsafe bool OnSend(ref byte[] data, ref int length)
        {
            m_Out += (uint)length;
            fixed (byte* ptr = data)
            {
                bool result = true;
                byte id = data[0];

                PacketReader reader = null;
                Packet packet = null;
                bool isView = PacketHandler.HasClientViewer(id);
                bool isFilter = PacketHandler.HasClientFilter(id);

                if (isView)
                {
                    reader = new PacketReader(ptr, length, PacketsTable.IsDynLength(id));
                    result = !PacketHandler.OnClientPacket(id, reader, packet);
                }
                else if (isFilter)
                {
                    packet = new Packet(data, length, PacketsTable.IsDynLength(id));
                    result = !PacketHandler.OnClientPacket(id, reader, packet);
                }
                //TODO: check if this is done correctly
                PacketLogger.SharedInstance.LogPacketData(PacketPath.ClientToServer, data, result);
                return result;
            }
        }

        private void OnMouseHandler(int button, int wheel)
        {
            if (button > 4)
                button = 3;
            else if (button > 3)
                button = 2;
            else if (button > 2)
                button = 2;
            else if (button > 1)
                button = 1;

            RazorEnhanced.HotKey.OnMouse(button, wheel);
        }

        private enum SDL_Keymod
        {
            KMOD_NONE = 0x0000,
            KMOD_LSHIFT = 0x0001,
            KMOD_RSHIFT = 0x0002,
            KMOD_LCTRL = 0x0040,
            KMOD_RCTRL = 0x0080,
            KMOD_LALT = 0x0100,
            KMOD_RALT = 0x0200,
            KMOD_LGUI = 0x0400,
            KMOD_RGUI = 0x0800,
            KMOD_NUM = 0x1000,
            KMOD_CAPS = 0x2000,
            KMOD_MODE = 0x4000,
            KMOD_RESERVED = 0x8000
        }

        private enum SDL_Keycode_Ignore
        {
            SDLK_LCTRL = 1073742048,
            SDLK_LSHIFT = 1073742049,
            SDLK_LALT = 1073742050,
            SDLK_RCTRL = 1073742052,
            SDLK_RSHIFT = 1073742053,
            SDLK_RALT = 1073742054,
        }

        // Weird situation where CUO was passing in a wrong value
        // for oem keys.
        // so, I special case those, check if down, and return appropriate
        // code
        internal int checkForOmeKeys(int key)
        {
            Keys[] oemKeys = {Keys.Oem1, Keys.Oem102, Keys.Oem2,
            Keys.Oem3, Keys.Oem4, Keys.Oem5, Keys.Oem6, Keys.Oem7, Keys.Oem8,
            Keys.OemBackslash, Keys.OemClear, Keys.OemCloseBrackets,
            Keys.Oemcomma, Keys.OemMinus, Keys.OemOpenBrackets,
            Keys.OemPeriod, Keys.OemPipe, Keys.Oemplus, Keys.OemQuestion,
            Keys.OemQuotes, Keys.OemSemicolon, Keys.Oemtilde };
            foreach (var oemKey in oemKeys)
            {
                if ((Platform.GetAsyncKeyState((int)oemKey) & 0xFF00) != 0)
                    return (int)oemKey;
            }
            return key;
        }

        private bool OnHotKeyHandler(int inkey, int mod, bool ispressed)
        {
            int key = checkForOmeKeys(inkey);
            if (ispressed && !Enum.IsDefined(typeof(SDL_Keycode_Ignore), key))
            {
                RazorEnhanced.ModKeys cur = RazorEnhanced.ModKeys.None;
                SDL_Keymod keymod = (SDL_Keymod)mod;
                if (keymod.HasFlag(SDL_Keymod.KMOD_LCTRL) || keymod.HasFlag(SDL_Keymod.KMOD_RCTRL))
                    cur |= RazorEnhanced.ModKeys.Control;
                if (keymod.HasFlag(SDL_Keymod.KMOD_LALT) || keymod.HasFlag(SDL_Keymod.KMOD_RALT))
                    cur |= RazorEnhanced.ModKeys.Alt;
                if (keymod.HasFlag(SDL_Keymod.KMOD_LSHIFT) || keymod.HasFlag(SDL_Keymod.KMOD_RSHIFT))
                    cur |= RazorEnhanced.ModKeys.Shift;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    return RazorEnhanced.HotKey.OnKeyDown(LinuxPlatform.MapKey(key), cur);
                return RazorEnhanced.HotKey.OnKeyDown(Win32Platform.MapKey(key), cur);
            }

            return true;
        }
        private void OnDisconnected()
        {
            base.OnDisconnected();
        }

        private void OnConnected()
        {
            if (_cuoBindingRejected)
            {
                return;
            }

            if (!VerifyCuoBinding(true, out string failure))
            {
                RejectCuoBinding(failure);
                return;
            }

            base.OnConnected();
            bool ReWindowVisible = false;
            foreach (Screen screen in Screen.AllScreens)
            {
                System.Drawing.Rectangle screenArea = new(screen.Bounds.Location, screen.Bounds.Size);
                screenArea.Width -= (Engine.MainWindow.Width / 2);
                screenArea.Height -= (Engine.MainWindow.Height / 2);
                if (screenArea.Contains(Engine.MainWindow.Location))
                {
                    ReWindowVisible = true;
                }
            }
            if (!ReWindowVisible)
            {
                System.Drawing.Rectangle cuoWindow = Client.Instance.GetUoWindowPos();
                Engine.MainWindow.SafeAction(s => { s.Location = cuoWindow.Location; });
            }

            m_ConnectionStart = DateTime.UtcNow;
            m_LastConnection = Engine.IP;
        }

        private void OnClientClosing()
        {
            Close();
        }

        private void OnInitialize()
        {
            if (_cuoBindingRejected)
            {
                return;
            }

            if (!VerifyCuoBinding(false, out string failure))
            {
                RejectCuoBinding(failure);
            }
        }

        private static bool VerifyCuoBinding(bool requireActiveSession, out string failure)
        {
            failure = null;

            if (TryVerifyNativeCuoBinding(requireActiveSession, out failure, out bool nativeHandled))
            {
                return true;
            }

            if (nativeHandled)
            {
                return false;
            }

            Assembly cuoAssembly = CUOAssembly;
            if (cuoAssembly == null)
            {
                failure = "无法读取 ClassicUO 主程序集。";
                return false;
            }

            Type bindingType = cuoAssembly.GetType("ClassicUO.Security.CuoBinding", false);
            if (bindingType == null)
            {
                failure = "当前 ClassicUO 不是绑定版，缺少绑定验证接口。";
                return false;
            }

            string fingerprint = ReadStaticString(bindingType, "SuoxPublicKeyFingerprint");
            if (!StringComparer.OrdinalIgnoreCase.Equals(fingerprint, ExpectedCuoSuoxFingerprint))
            {
                failure = "ClassicUO 绑定密钥指纹不匹配。";
                return false;
            }

            byte version = ReadStaticByte(bindingType, "SuoxVersion");
            if (version != ExpectedCuoSuoxVersion)
            {
                failure = "ClassicUO 绑定协议版本不匹配。";
                return false;
            }

            ushort buildId = ReadStaticUInt16(bindingType, "SuoxBuildId");
            if (buildId != ExpectedCuoSuoxBuildId)
            {
                failure = "ClassicUO 绑定构建号不匹配。";
                return false;
            }

            if (requireActiveSession && !ReadStaticBool(bindingType, "SuoxActive"))
            {
                failure = "ClassicUO 尚未通过服务器绑定握手。";
                return false;
            }

            return true;
        }

        private static bool TryVerifyNativeCuoBinding(bool requireActiveSession, out string failure, out bool handled)
        {
            failure = null;
            handled = false;

            IntPtr cuoModule = GetModuleHandle("cuo.dll");
            if (cuoModule == IntPtr.Zero)
            {
                return false;
            }

            IntPtr magicPtr = GetProcAddress(cuoModule, "CuoBinding_GetMagic");
            IntPtr statusPtr = GetProcAddress(cuoModule, "CuoBinding_GetStatus");
            if (magicPtr == IntPtr.Zero || statusPtr == IntPtr.Zero)
            {
                return false;
            }

            handled = true;

            CuoBindingNativeCall getMagic =
                (CuoBindingNativeCall)Marshal.GetDelegateForFunctionPointer(magicPtr, typeof(CuoBindingNativeCall));
            CuoBindingNativeCall getStatus =
                (CuoBindingNativeCall)Marshal.GetDelegateForFunctionPointer(statusPtr, typeof(CuoBindingNativeCall));

            if (getMagic() != ExpectedCuoBindingMagic)
            {
                failure = "ClassicUO 绑定密钥指纹不匹配。";
                return false;
            }

            int status = getStatus();
            if ((status & 0x01) == 0)
            {
                failure = "当前 ClassicUO 不是绑定版，缺少绑定验证接口。";
                return false;
            }

            byte version = (byte)((status >> 8) & 0xFF);
            if (version != ExpectedCuoSuoxVersion)
            {
                failure = "ClassicUO 绑定协议版本不匹配。";
                return false;
            }

            ushort buildId = (ushort)((status >> 16) & 0xFFFF);
            if (buildId != ExpectedCuoSuoxBuildId)
            {
                failure = "ClassicUO 绑定构建号不匹配。";
                return false;
            }

            if (requireActiveSession && (status & 0x02) == 0)
            {
                failure = "ClassicUO 尚未通过服务器绑定握手。";
                return false;
            }

            return true;
        }

        private static string ReadStaticString(Type type, string name)
        {
            object value = ReadStaticMember(type, name);
            return value as string;
        }

        private static byte ReadStaticByte(Type type, string name)
        {
            object value = ReadStaticMember(type, name);
            return value == null ? (byte)0 : Convert.ToByte(value);
        }

        private static ushort ReadStaticUInt16(Type type, string name)
        {
            object value = ReadStaticMember(type, name);
            return value == null ? (ushort)0 : Convert.ToUInt16(value);
        }

        private static bool ReadStaticBool(Type type, string name)
        {
            object value = ReadStaticMember(type, name);
            return value != null && Convert.ToBoolean(value);
        }

        private static object ReadStaticMember(Type type, string name)
        {
            BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
            FieldInfo field = type.GetField(name, flags);
            if (field != null)
            {
                return field.GetValue(null);
            }

            PropertyInfo property = type.GetProperty(name, flags);
            if (property != null)
            {
                return property.GetValue(null, null);
            }

            return null;
        }

        private static void RejectCuoBinding(string reason)
        {
            if (_cuoBindingRejected)
            {
                return;
            }

            _cuoBindingRejected = true;

            HideMainWindowForBindingReject();

            try
            {
                RazorEnhanced.UI.RE_MessageBox.Show("客户端验证失败",
                    "RA和当前客户端不匹配，RA不可以启动。\r\n\r\n请使用阳光大陆专用客户端登录游戏，才能使用这个RA。",
                    ok: "确定", no: null, cancel: null, backColor: null);
            }
            catch
            {
            }

            CloseMainWindowForBindingReject();
        }

        private static void HideMainWindowForBindingReject()
        {
            try
            {
                Engine.MainWindow?.SafeAction(s =>
                {
                    s.Hide();
                });
            }
            catch
            {
            }
        }

        private static void CloseMainWindowForBindingReject()
        {
            try
            {
                Engine.MainWindow?.SafeAction(s =>
                {
                    s.CanClose = true;
                    s.Close();
                });
            }
            catch
            {
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int CuoBindingNativeCall();

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        public override void SetConnectionInfo(IPAddress addr, int port)
        {
        }

        public override void SetNegotiate(bool negotiate)
        {
        }

        public override bool Attach(int pid)
        {
            return false;
        }

        public override void Close()
        {
            base.Close();
        }

        public override void UpdateTitleBar()
        {
        }


        public override void SetTitleStr(string str)
        {
            //_setTitle(str);
        }

        public override bool OnMessage(MainForm razor, uint wParam, int lParam)
        {
            return false;
        }

        public override bool OnCopyData(IntPtr wparam, IntPtr lparam)
        {
            return false;
        }

        public override void SendToServer(Packet p)
        {
            byte[] data = p.Compile();
            int length = (int)p.Length;
            _sendToServer(ref data, ref length);
        }

        public override void SendToServer(PacketReader pr)
        {
            SendToServer(MakePacketFrom(pr));
        }

        public override void SendToClient(Packet p)
        {
            byte[] data = p.Compile();
            int length = (int)p.Length;

            _sendToClient(ref data, ref length);
        }

        public override void ForceSendToClient(Packet p)
        {
            byte[] data = p.Compile();
            int length = (int)p.Length;

            _sendToClient(ref data, ref length);
        }

        public override void ForceSendToServer(Packet p)
        {
            byte[] data = p.Compile();
            int length = (int)p.Length;

            _sendToServer(ref data, ref length);
        }

        public override void SetPosition(uint x, uint y, uint z, byte dir)
        {
        }

        public override string GetClientVersion()
        {
            return m_ClientVersion.ToString();
        }

        public override string GetUoFilePath()
        {
            return _uoFilePath();
        }

        public override IntPtr GetWindowHandle()
        {
            if (Environment.OSVersion.Platform == PlatformID.Unix ||
                Environment.OSVersion.Platform == PlatformID.MacOSX)
            {
                return m_ClientWindow;
            }

            using Process currentProcess = Process.GetCurrentProcess();
            uint currentProcessId = (uint)currentProcess.Id;

            if (IsClassicUoWindow(m_ClientWindow, currentProcessId))
                return m_ClientWindow;

            m_ClientWindow = FindClassicUoWindow(currentProcessId);
            return m_ClientWindow;
        }

        private static bool IsClassicUoWindow(IntPtr window, uint processId)
        {
            const uint GW_OWNER = 4;
            const int GWL_EXSTYLE = -20;
            const int WS_EX_TOOLWINDOW = 0x00000080;

            if (!IsWindow(window) ||
                GetWindow(window, GW_OWNER) != IntPtr.Zero ||
                (GetWindowLong(window, GWL_EXSTYLE) & WS_EX_TOOLWINDOW) != 0)
            {
                return false;
            }

            GetWindowThreadProcessId(window, out uint ownerProcessId);
            if (ownerProcessId != processId)
                return false;

            StringBuilder classNameBuffer = new(256);
            GetClassName(window, classNameBuffer, classNameBuffer.Capacity);
            string className = classNameBuffer.ToString();
            if (className.StartsWith("WindowsForms10.", StringComparison.OrdinalIgnoreCase))
                return false;

            StringBuilder titleBuffer = new(256);
            GetWindowText(window, titleBuffer, titleBuffer.Capacity);
            return className.IndexOf("SDL", StringComparison.OrdinalIgnoreCase) >= 0 ||
                titleBuffer.ToString().IndexOf("ClassicUO", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static IntPtr FindClassicUoWindow(uint processId)
        {
            IntPtr bestWindow = IntPtr.Zero;
            long bestScore = long.MinValue;

            EnumWindowsCallback callback = (window, parameter) =>
            {
                if (!IsWindowVisible(window) || !IsClassicUoWindow(window, processId))
                {
                    return true;
                }

                if (!GetWindowRect(window, out WindowRect bounds))
                    return true;

                int width = bounds.Right - bounds.Left;
                int height = bounds.Bottom - bounds.Top;
                if (width <= 0 || height <= 0)
                    return true;

                StringBuilder classNameBuffer = new(256);
                GetClassName(window, classNameBuffer, classNameBuffer.Capacity);
                string className = classNameBuffer.ToString();

                long score = (long)width * height;
                if (className.IndexOf("SDL", StringComparison.OrdinalIgnoreCase) >= 0)
                    score += 1L << 60;

                StringBuilder titleBuffer = new(256);
                GetWindowText(window, titleBuffer, titleBuffer.Capacity);
                if (titleBuffer.ToString().IndexOf("ClassicUO", StringComparison.OrdinalIgnoreCase) >= 0)
                    score += 1L << 59;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestWindow = window;
                }

                return true;
            };

            EnumWindows(callback, IntPtr.Zero);
            GC.KeepAlive(callback);
            return bestWindow;
        }

        public override uint TotalDataIn()
        {
            return m_In;
        }

        public override uint TotalDataOut()
        {
            return m_Out;
        }

        internal override bool RequestWalk(Direction m_Dir)
        {
            bool result = false;
            int MaxTries = 10;
            int Delay = 30;
            while (result == false)
            {
                MaxTries -= 1;
                result = _requestMove((int)m_Dir, false);
                if (result == false)
                    Thread.Sleep(Delay);
                if (MaxTries <= 0)
                    break;
            }
            return result;
        }
        internal override bool RequestRun(Direction m_Dir)
        {
            bool result = false;
            int MaxTries = 10;
            int Delay = 30;
            while (result == false)
            {
                MaxTries -= 1;
                result = _requestMove((int)m_Dir, true);
                if (result == false)
                    Thread.Sleep(Delay);
                if (MaxTries <= 0)
                    break;
            }
            return result;
        }
        public override void PathFindTo(Assistant.Point3D Location)
        {
            Assistant.Client.Instance.SendToClientWait(new PathFindTo(Location));
        }

        public void OnFocusGained()
        {
        }

        public void OnFocusLost()
        {
        }
        public override unsafe void SendToClientWait(Packet p)
        {
            SendToClient(p);
        }
        public override unsafe void SendToServerWait(Packet p)
        {
            SendToServer(p);
        }
        public override void BeginCalibratePosition()
        { }
        public override void InitSendFlush()
        { }
        public override bool Ready { get { return m_Ready; } }

        public override List<string> ValidFileLocations()
        {
            List<string> validFileLocations = new();
            validFileLocations.Add(Assistant.Engine.RootPath);
            validFileLocations.Add(Path.GetDirectoryName(CUOAssembly.Location));

            return validFileLocations;
        }
        public override int GetBuildPart()
        {
            return m_ClientVersion.Build;
        }

        public override int GetMajorPart()
        {
            return m_ClientVersion.Major;
        }

    }
}
