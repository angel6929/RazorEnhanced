using Accord.Math;
using Assistant;
using FastColoredTextBoxNS;
using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using Microsoft.Scripting.Utils;
using RazorEnhanced.UOS;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace RazorEnhanced.UI
{
    internal partial class EnhancedScriptEditor : Form
    {
        private delegate void SetHighlightLineDelegate(int iline, Color color);

        private delegate void SetStatusLabelDelegate(string text, Color color);

        private delegate void SetRecordButtonDelegate(string text);

        private delegate string GetFastTextBoxTextDelegate();

        private delegate void SetTracebackDelegate(string text);

        private enum Command
        {
            None = 0,
            Line,
            Call,
            Return,
            Breakpoint
        }


        private static List<EnhancedScriptEditor> m_EnhancedScriptEditors = new();


        public static EnhancedScriptEditor Search(string fullpath)
        {
            foreach (var editor in m_EnhancedScriptEditors)
            {
                if (editor.Script != null && editor.Script.Fullpath == fullpath)
                {
                    return editor;
                }
            }
            return null;
        }

        private static ConcurrentQueue<Command> m_Queue = new();
        private static Command m_CurrentCommand = Command.None;
        private static readonly AutoResetEvent m_WaitDebug = new(false);




        //private string m_Filename = String.Empty;
        //private string m_Filetype = String.Empty;
        //public static ScriptLanguage GetScriptLanguage() {  return LatestEditor.m_Script.GetLanguage(); }

        //private string m_Filepath = String.Empty;

        //private readonly PythonEngine m_pe;

        private TraceBackFrame m_CurrentFrame;
        private FunctionCode m_CurrentCode;
        private string m_CurrentResult;
        private object m_CurrentPayload;



        //Dalamar:
        //TODO: replace current implementation with 
        private EnhancedScript m_Script;
        public EnhancedScript Script { get { return m_Script; } }
        private ScriptRecorder m_Recorder;

        private readonly List<int> m_Breakpoints = new();

        private volatile bool m_Debugger = false;
        private bool m_onclosing = false;

        private bool m_ScriptWasRunning = false;
        private System.Threading.Timer m_Timer;

        private readonly FastColoredTextBoxNS.AutocompleteMenu m_popupMenu;



        internal static bool Init(string filePath)
        {
            string envEditorName = Environment.GetEnvironmentVariable("EDITOR");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && string.IsNullOrEmpty(envEditorName))
            {
                Utility.Logger.Debug("EDITOR environment variable is not set.");
                return false;
            }

            if (!string.IsNullOrEmpty(envEditorName))
            {
                Utility.Logger.Debug($"EnhancedScriptEditor launchind editor for filePath={filePath}");
                try
                {
                    if (filePath == null)
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = envEditorName,
                            UseShellExecute = true,
                            CreateNoWindow = true
                        });
                    }
                    else
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = envEditorName,
                            Arguments = $"\"{filePath}\"",
                            UseShellExecute = true,
                            CreateNoWindow = true
                        });

                    }
                    return true;
                }
                catch (Exception ex)
                {
                    Utility.Logger.Debug($"Error launching editor: {ex.Message}");
                    return false;
                }
            }
            else
            {
                string suffix = Path.GetExtension(filePath); ;
                if (suffix == null)
                {
                    ScriptListView scriptListView = MainForm.GetCurrentAllScriptsTab();
                    if (scriptListView != null)
                    {
                        if (scriptListView.Name == "pyScriptListView")
                            suffix = ".py";
                        if (scriptListView.Name == "uosScriptListView")
                            suffix = ".uos";
                        if (scriptListView.Name == "csScriptListView")
                            suffix = ".cs";
                    }
                }

                var editor = EnhancedScriptEditor.Search(filePath);
                if (editor == null)
                {
                    editor = new EnhancedScriptEditor(filePath, suffix);
                    editor.Show();
                }
                editor.BringToFront();
                return true;
            }
        }


        private void OnLoad()
        {
            var TimerDelay = 100;
            var TimerTick = new System.Threading.TimerCallback(OnRefresh);
            m_Timer = new System.Threading.Timer(TimerTick, null, TimerDelay, TimerDelay);

            toolStripStatusLabelScript.Width = this.Width - 20;
            SetStatusLabel("IDLE", Color.DarkTurquoise);
        }

        private bool OnUnload()
        {
            if (!CloseAndSave()) return false;
            m_EnhancedScriptEditors.Remove(this);
            m_Timer.Change(Timeout.Infinite, Timeout.Infinite);
            m_Timer = null;
            return true;
        }

        private void OnRefresh(object state)
        {
            if (m_Script == null) { return; }
            if (!m_Script.IsRunning && m_ScriptWasRunning)
            {
                SetStatusLabel("STOP", Color.DarkTurquoise);
            }
            else if (m_Script.IsRunning && !m_ScriptWasRunning)
            {
                if (m_Debugger)
                {
                    SetErrorBox("DEBUG: " + m_Script.Fullpath);
                    SetStatusLabel("DEBUGGER ACTIVE", Color.YellowGreen);
                }
                else
                {
                    SetErrorBox("RUN: " + m_Script.Fullpath);
                    SetStatusLabel("SCRIPT RUNNING", Color.Green);
                }
            }

            m_ScriptWasRunning = m_Script.IsRunning;
        }


        /*
        internal static void End()
        {
            if (m_EnhancedScriptEditors.Count > 0)
            {
                //m_Recorder.Recording = false;
                if (ScriptRecorder.OnRecord)
                    ScriptRecorder.OnRecord = false;

                LatestEditor.Stop();
            }
        }
        */

        public bool LoadFromFile(string filepath)
        {
            if (!File.Exists(filepath)) { return false; }

            if (m_Script != null && m_Script.Editor)
            {
                EnhancedScript.Service.RemoveScript(m_Script);
            }

            Action<string> stdout = null;
            Action<string> stderr = null;
            if (m_Script != null)
            {
                stdout = m_Script.ScriptEngine.GetStdout();
                stderr = m_Script.ScriptEngine.GetStderr();
            }
            m_Script = EnhancedScript.FromFile(filepath, editor: true);
            if (m_Script != null)
            {
                m_Script.ScriptEngine.SetStdout(stdout);
                m_Script.ScriptEngine.SetStderr(stderr);
            }

            var language = m_Script.GetLanguage();
            LoadLanguage(language);
            fastColoredTextBoxEditor.Text = m_Script.Text;
            fastColoredTextBoxEditor.IsChanged = false;
            UpdateTitle();
            return true;
        }

        public void LoadNewFile(ScriptLanguage language)
        {
            m_Script = EnhancedScript.FromText("", language);
            language = m_Script.GetLanguage();
            LoadLanguage(language);
            fastColoredTextBoxEditor.Text = "";
            fastColoredTextBoxEditor.IsChanged = false;
            UpdateTitle();
        }


        internal EnhancedScriptEditor(string filename, string filetype = ".py")
        {
            InitializeComponent();
            //Automenu Section
            m_popupMenu = new AutocompleteMenu(fastColoredTextBoxEditor);
            m_popupMenu.Items.ImageList = imageList2;
            m_popupMenu.SearchPattern = @"[\w\.:=!<>]";
            m_popupMenu.AllowTabKey = true;
            m_popupMenu.ToolTipDuration = 5000;
            m_popupMenu.AppearInterval = 100;

            if (!LoadFromFile(filename))
            {
                var language = EnhancedScript.ExtToLanguage(filetype);
                LoadNewFile(language);
            }

            if (m_Script.GetLanguage() == ScriptLanguage.PYTHON)
            {
                m_Script.ScriptEngine.SetTracebackPython(null);
            }
            m_Script.ScriptEngine.SetStdout(this.SetErrorBox);
            m_Script.ScriptEngine.SetStderr(this.SetErrorBox);
            // Always have to make these or Open() wont work from UOS to PY
            // m_pe = new PythonEngine(this.SetErrorBox);
            // m_pe.Engine.SetTrace(null);
            UpdateTitle();

            m_EnhancedScriptEditors.Add(this);
        }

        public void LoadLanguage(ScriptLanguage language = ScriptLanguage.UNKNOWN)
        {
            switch (language)
            {
                default:
                case ScriptLanguage.PYTHON:
                    fastColoredTextBoxEditor.Language = FastColoredTextBoxNS.Language.Python;
                    InitPythonSyntaxHighlight();
                    break;
                case ScriptLanguage.CSHARP:
                    fastColoredTextBoxEditor.Language = FastColoredTextBoxNS.Language.CSharp;
                    break;
                case ScriptLanguage.UOSTEAM:
                    fastColoredTextBoxEditor.Language = FastColoredTextBoxNS.Language.Uos;
                    fastColoredTextBoxEditor.AutoIndentExistingLines = true;
                    UpdateSyntaxHighlight();
                    break;
            }
        }

        private void UpdateTitle()
        {
            var title = "Enhanced Script Editor";
            if (World.Player != null)
            {
                if (m_Script != null && m_Script.Fullpath != String.Empty)
                {
                    title = String.Format("{0} ({1}) - {2}", World.Player.Name, World.ShardName, m_Script.Fullpath);
                }
                else
                {
                    title = String.Format("Enhanced Script Editor - {0} ({1})", World.Player.Name, World.ShardName);
                }
            }

            this.Text = title;
        }


        public void UpdateSyntaxHighlight()
        {
            // keywords
            var uosEngine = new UOSteamEngine();
            if (uosEngine == null) return;

            List<String> keywords = uosEngine.AllKeywords();
            string[] syntax =
            {
                "and", "break", "continue", "elif", "else", "for", "if", "not", "or", "while", "true", "false",
                "endif", "endwhile", "endfor"
            };
            keywords.AddRange(syntax);
            String pattern = $@"\b({String.Join("|", keywords)})\b";
            this.fastColoredTextBoxEditor.SyntaxHighlighter.UosKeywordRegex = new Regex(pattern, RegexOptions.Compiled);

            // attributes
            List<String> aliases = uosEngine.AllAliases();
            pattern = $@"\b({String.Join("|", aliases)})\b";
            this.fastColoredTextBoxEditor.SyntaxHighlighter.UosAttributeRegex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

            // Fill in autocomplete
            List<AutocompleteItem> items = new();
            keywords = keywords.Distinct().ToList();
            keywords.Sort();
            foreach (var item in keywords)
            {
                items.Add(new AutocompleteItem(item) { ImageIndex = 0 });
            }
            m_popupMenu.Items.SetAutocompleteItems(items);

            //Increase the width for individual items so that the entire name is visible
            m_popupMenu.Items.MaximumSize = new Size(m_popupMenu.Items.Width + 400, m_popupMenu.Items.Height);
            m_popupMenu.Items.Width = m_popupMenu.Items.Width + 400;

            //
            ToolTipDescriptions tooltip;
            var methods = keywords.ToArray();
            var autodocMethods = new Dictionary<string, ToolTipDescriptions>();
            foreach (var method in keywords)
            {
                var methodName = method;
                var prms_name = new List<String>();
                var prms_type = new List<String>();
                var prms_name_type = new List<String>();
                var prms_name_type_desc = new List<String>();
                prms_name_type_desc.Add("Param1");
                prms_name_type_desc.Add("Param2");

                if (!autodocMethods.ContainsKey(method))
                {
                    //tooltip = new ToolTipDescriptions(method, prms_name_type_desc.ToArray(), method.returnType, method.itemDescription.Trim() + "\n");
                    tooltip = new ToolTipDescriptions(method, prms_name_type_desc.ToArray(), "ret", "desc" + "\n");
                    autodocMethods.Add(method, tooltip);
                }
            }
        }


        public void InitPythonSyntaxHighlight()
        {
            //Dalamar: Trying to inject SyntaxHighlight (and Autocomplete) from AutoDoc
            //TODO: make it work
            // # Syntax Highlight
            List<String> itemList;
            List<String> escaped;
            String pattern;
            // ## Classes
            itemList = AutoDoc.GetClasses();
            escaped = new List<String>();
            foreach (var name in itemList)
            {
                escaped.Add(Regex.Escape(name));
            }
            pattern = $@"\b({String.Join("|", escaped)})\b";
            this.fastColoredTextBoxEditor.SyntaxHighlighter.RazorClassKeywordRegex = new Regex(pattern, RegexOptions.Compiled);

            // ## Properties
            itemList = AutoDoc.GetProperties();
            escaped = new List<String>();
            foreach (var name in itemList)
            {
                escaped.Add(Regex.Escape(name));
            }
            pattern = $@"\b({String.Join("|", escaped)})\b";
            this.fastColoredTextBoxEditor.SyntaxHighlighter.RazorPropsKeywordRegex = new Regex(pattern, RegexOptions.Compiled);

            // ## Functions
            itemList = AutoDoc.GetMethods();
            escaped = new List<String>();
            foreach (var name in itemList)
            {
                escaped.Add(Regex.Escape(name));
            }
            pattern = $@"\b({String.Join("|", escaped)})\b";
            this.fastColoredTextBoxEditor.SyntaxHighlighter.RazorFunctionsKeywordRegex = new Regex(pattern, RegexOptions.Compiled);

            #region Keywords

            string[] keywords =
            {
                "and", "assert", "break", "class", "continue", "def", "del", "elif", "else", "except", "exec",
                "finally", "for", "from", "global", "if", "import", "in", "is", "lambda", "not", "or", "pass", "print",
                "raise", "return", "try", "while", "yield", "None", "True", "False", "as", "sorted", "filter"
            };

            #endregion

            #region Classes Autocomplete



            //Dalamar: AutoDoc
            string[] classes = AutoDoc.GetClasses().ToArray();

            //Dalamar: AutoDoc
            string[] methods = AutoDoc.GetMethods(true, true, false).ToArray();

            #region Props Autocomplete

            string[] propsPlayer =
            {
                "Player.StatCap", "Player.AR", "Player.FireResistance", "Player.ColdResistance", "Player.EnergyResistance",
                "Player.PoisonResistance", "Player.StaticMount",
                "Player.Buffs", "Player.IsGhost", "Player.Female", "Player.Name", "Player.Bank",
                "Player.Gold", "Player.Luck", "Player.Body", "Player.HasSpecial",
                "Player.Followers", "Player.FollowersMax", "Player.MaxWeight", "Player.Str", "Player.Dex", "Player.Int",
            };

            string[] propsPositions =
            {
                "Position.X", "Position.Y", "Position.Z"
            };

            string[] propsWithCheck = AutoDoc.GetProperties(true).Union(propsPlayer).Union(propsPositions).ToArray();

            string[] propsGeneric =
            {
                "Serial", "Hue", "Name", "Body", "Color", "Direction", "Visible", "Poisoned", "YellowHits", "Paralized",
                "Human", "WarMode", "Female", "Hits", "HitsMax", "Stam", "StamMax", "Mana", "ManaMax", "Backpack", "Mount",
                "Quiver", "Notoriety", "Map", "InParty", "Properties", "Amount", "IsBagOfSending", "IsContainer", "IsCorpse",
                "IsDoor", "IsInBank", "Movable", "OnGround", "ItemID", "RootContainer", "Container", "Durability", "MaxDurability",
                "Contains", "Weight", "Position", "StaticID", "StaticHue", "StaticZ", "Flying",

                // Item filter part
                "Enabled", "Graphics", "Hues", "RangeMin", "RangeMax", "Layers",
                // Mobiles
                "Bodies", "Notorieties", "CheckIgnoreObject",
                // PathFind
                "DebugMessage", "StopIfStuck", "MaxRetry", "Timeout"
            };

            string[] props = AutoDoc.GetProperties().Union(propsGeneric).ToArray();

            #endregion
            #endregion


            ToolTipDescriptions tooltip;

            var autodocMethods = new Dictionary<string, ToolTipDescriptions>();
            foreach (var docitem in AutoDoc.GetPythonAPI().methods)
            {
                var method = docitem;
                var methodName = method.itemClass + "." + method.itemName;
                var prms_name = new List<String>();
                var prms_type = new List<String>();
                var prms_name_type = new List<String>();
                var prms_name_type_desc = new List<String>();
                foreach (var prm in method.paramList)
                {
                    prms_name.Add(prm.itemName);
                    prms_type.Add(prm.itemType);


                    string name_type = $"{prm.itemName}: {prm.itemType}";
                    prms_name_type.Add(name_type);

                    string name_type_desc = name_type;
                    if (prm.itemDescription.Trim().Length > 0)
                    {
                        name_type_desc += $"\n    {prm.itemDescription.Trim()}";
                    }
                    prms_name_type_desc.Add(name_type_desc);
                }
                var methodSignNames = $"{methodName}({String.Join(",", prms_name)})";
                var methodSignTypes = $"{methodName}({String.Join(",", prms_type)})";
                var methodSignNameTypes = $"{methodName}({String.Join(",", prms_name_type)})";

                var methodKey = methodSignNames;

                if (!autodocMethods.ContainsKey(methodKey))
                {
                    tooltip = new ToolTipDescriptions(methodSignNames, prms_name_type_desc.ToArray(), method.returnType, method.itemDescription.Trim() + "\n");
                    autodocMethods.Add(methodKey, tooltip);
                }
                else
                {
                    autodocMethods[methodKey].Notes += "\n" + methodSignNameTypes;
                    if (method.itemDescription.Length > 0)
                    {
                        autodocMethods[methodKey].Notes += "\n    " + method.itemDescription.Trim() + "\n";
                    }
                }

            }

            var descriptionMethods = autodocMethods.ToDictionary(x => x.Key, x => x.Value);


            List<AutocompleteItem> items = new();

            //Permette la creazione del menu con la singola keyword
            Array.Sort(keywords);
            foreach (var item in keywords)
                items.Add(new AutocompleteItem(item) { ImageIndex = 0 });
            //Permette la creazione del menu con la singola classe
            Array.Sort(classes);
            foreach (var item in classes)
                items.Add(new AutocompleteItem(item) { ImageIndex = 1 });

            //Permette di creare il menu solo per i metodi della classe digitata
            Array.Sort(methods);
            foreach (var item in methods)
            {
                descriptionMethods.TryGetValue(item, out ToolTipDescriptions element);

                if (element != null)
                {
                    items.Add(new MethodAutocompleteItemAdvance(item)
                    {
                        ImageIndex = 2,
                        ToolTipTitle = element.Title,
                        ToolTipText = element.ToolTipDescription()
                    });
                }
                else
                {
                    items.Add(new MethodAutocompleteItemAdvance(item)
                    {
                        ImageIndex = 2
                    });
                }
            }

            //Permette di creare il menu per le props solo sulla classe Player
            Array.Sort(propsWithCheck);
            foreach (var item in propsWithCheck)
                items.Add(new SubPropertiesAutocompleteItem(item) { ImageIndex = 4 });

            //Props generiche divise tra quelle Mobiles e Items, che possono
            //Appartenere a variabili istanziate di una certa classe
            //Qui sta alla cura dell'utente capire se una props va bene o no
            //Per quella istanza
            Array.Sort(props);
            foreach (var item in props)
                items.Add(new MethodAutocompleteItem(item) { ImageIndex = 5 });

            m_popupMenu.Items.SetAutocompleteItems(items);

            //Aumenta la larghezza per i singoli item, in modo che l'intero nome sia visibile
            m_popupMenu.Items.MaximumSize = new Size(m_popupMenu.Items.Width + 20, m_popupMenu.Items.Height);
            m_popupMenu.Items.Width = m_popupMenu.Items.Width + 20;

        }
        private void OnTraceback(string line, int line_num)
        {

        }

        private void OnTracebackUOS(TraceBackFrame frame, string result, object payload)
        {

        }

        private TracebackDelegate OnTracebackPy(TraceBackFrame frame, string result, object payload)
        {
            if (m_Debugger)
            {
                m_WaitDebug.WaitOne();
                CheckCurrentCommand();

                if (m_CurrentCommand != Command.None)
                {
                    UpdateCurrentState(frame, result, payload);
                    int line = (int)m_CurrentFrame.f_lineno;

                    switch (m_CurrentCommand)
                    {
                        case Command.Breakpoint:

                            if (m_Breakpoints.Contains(line))
                                TracebackBreakpoint();
                            else
                                EnqueueCommand(Command.Breakpoint);
                            break;

                        case Command.Call:

                            if (result == "call")
                                TracebackCall();
                            else
                                EnqueueCommand(Command.Call);
                            break;

                        case Command.Line:

                            if (result == "line")
                                TracebackLine();
                            else
                                EnqueueCommand(Command.Line);
                            break;

                        case Command.Return:

                            if (result == "return")
                                TracebackReturn();
                            else
                                EnqueueCommand(Command.Return);
                            break;
                    }
                }

                return OnTracebackPy;
            }
            else
                return null;
        }

        private void TracebackCall()
        {
            SetStatusLabel("DEBUGGER ACTIVE - " + string.Format("Call {0}", m_CurrentCode.co_name), Color.YellowGreen);
            SetHighlightLine((int)m_CurrentFrame.f_lineno - 1, Color.LightGreen);
            string locals = GetLocalsText(m_CurrentFrame);
            SetTracebackOutput(locals);
        }

        private void TracebackReturn()
        {
            SetStatusLabel("DEBUGGER ACTIVE - " + string.Format("Return {0}", m_CurrentCode.co_name), Color.YellowGreen);
            SetHighlightLine((int)m_CurrentFrame.f_lineno - 1, Color.LightBlue);
            string locals = GetLocalsText(m_CurrentFrame);
            SetTracebackOutput(locals);
        }

        private void TracebackLine()
        {
            SetStatusLabel("DEBUGGER ACTIVE - " + string.Format("Line {0}", (int)m_CurrentFrame.f_lineno), Color.YellowGreen);
            SetHighlightLine((int)m_CurrentFrame.f_lineno - 1, Color.Yellow);
            string locals = GetLocalsText(m_CurrentFrame);
            SetTracebackOutput(locals);
        }

        private void TracebackBreakpoint()
        {
            SetStatusLabel("DEBUGGER ACTIVE - " + string.Format("Breakpoint at line {0}", (int)m_CurrentFrame.f_lineno), Color.YellowGreen);
            string locals = GetLocalsText(m_CurrentFrame);
            SetTracebackOutput(locals);
        }

        private void EnqueueCommand(Command command)
        {
            m_Queue.Enqueue(command);
            m_WaitDebug.Set();
        }

        private bool CheckCurrentCommand()
        {
            m_CurrentCommand = Command.None;
            bool result = m_Queue.TryDequeue(out m_CurrentCommand);
            return result;
        }

        private void UpdateCurrentState(TraceBackFrame frame, string result, object payload)
        {
            m_CurrentFrame = frame;
            m_CurrentCode = frame.f_code;
            m_CurrentResult = result;
            m_CurrentPayload = payload;
        }

        private void Start(bool debugger)
        {
            if (autoclearToolStripMenuItem.Checked)
            {
                outputConsole.Clear();
            }

            if (World.Player == null)
            {
                SetErrorBox("ERROR: Can't start script if not logged in game.");
                return;
            }

            if (m_Recorder != null && m_Recorder.IsRecording())
            {
                SetErrorBox("ERROR: Can't start script if record mode is ON.");
                return;
            }

            m_Debugger = debugger;


            m_Queue = new ConcurrentQueue<Command>();

            string text = GetFastTextBoxText();
            m_Script.Text = text;
            m_Script.LastModified = DateTime.Now;
            if (!m_Script.InitEngine()) { return; }

            //Editor specific setup for each language Check 
            switch (m_Script.Language)
            {
                default:
                case ScriptLanguage.PYTHON:
                    m_Script.ScriptEngine.SetTracebackPython(OnTracebackPy);
                    break;
                case ScriptLanguage.CSHARP:
                    if (m_Script.HasValidPath)
                    {
                        Save();
                        SetErrorBox("SAVE: " + m_Script.Fullpath);
                    }
                    else
                    {
                        SetErrorBox("ERROR: Due to a limitation, C# scripts must be saved before run it");
                        throw new Exception();
                    }
                    break;
            }
            m_Script.Start();
        }

        private void Stop()
        {
            if (m_Recorder != null && m_Recorder.IsRecording()) return;

            m_Debugger = false;
            m_Queue = new ConcurrentQueue<Command>();
            m_Breakpoints.Clear();

            for (int iline = 0; iline < fastColoredTextBoxEditor.LinesCount; iline++)
            {
                fastColoredTextBoxEditor[iline].BackgroundBrush = new SolidBrush(Color.White);
            }
            fastColoredTextBoxEditor.Invalidate();

            SetStatusLabel("IDLE", Color.DarkTurquoise);
            SetTracebackOutput(String.Empty);

            m_Script.Stop();
            SetErrorBox("STOP: " + m_Script.Fullpath);
        }

        private void SetHighlightLine(int iline, Color background)
        {
            if (this.m_onclosing)
                return;

            if (this.fastColoredTextBoxEditor.InvokeRequired)
            {
                SetHighlightLineDelegate d = new(SetHighlightLine);
                this.Invoke(d, new object[] { iline, background });
            }
            else
            {
                for (int i = 0; i < fastColoredTextBoxEditor.LinesCount; i++)
                {
                    if (m_Breakpoints.Contains(i))
                        fastColoredTextBoxEditor[i].BackgroundBrush = new SolidBrush(Color.Red);
                    else
                        fastColoredTextBoxEditor[i].BackgroundBrush = new SolidBrush(Color.White);
                }

                this.fastColoredTextBoxEditor[iline].BackgroundBrush = new SolidBrush(background);
                this.fastColoredTextBoxEditor.Invalidate();
            }
        }

        private void SetStatusLabel(string text, Color color)
        {
            if (this.m_onclosing || this.Disposing)
                return;
            try
            {
                if (this.InvokeRequired)
                {
                    SetStatusLabelDelegate d = new(SetStatusLabel);
                    this.Invoke(d, new object[] { text, color });
                }
                else
                {
                    this.toolStripStatusLabelScript.Text = "--> " + text;
                    this.statusStrip1.BackColor = color;

                }
            }
            catch { }
        }

        private void SetRecordButton(string text)
        {
            if (this.m_onclosing || this.Disposing)
                return;

            if (this.InvokeRequired)
            {
                SetRecordButtonDelegate d = new(SetRecordButton);
                this.Invoke(d, new object[] { text });
            }
            else
            {
                toolStripButtonGumps.Text = text;
            }
        }

        private string GetFastTextBoxText()
        {
            if (this.fastColoredTextBoxEditor.InvokeRequired)
            {
                GetFastTextBoxTextDelegate d = new(GetFastTextBoxText);
                return (string)this.Invoke(d, null);
            }
            else
            {
                return fastColoredTextBoxEditor.Text;
            }
        }

        private string GetLocalsText(TraceBackFrame frame)
        {
            string result = String.Empty;

            PythonDictionary locals = frame.f_locals as PythonDictionary;
            if (locals != null)
            {
                foreach (KeyValuePair<object, object> pair in locals)
                {
                    if (!(pair.Key.ToString().StartsWith("__") && pair.Key.ToString().EndsWith("__")))
                    {
                        string line = pair.Key.ToString() + ": " + (pair.Value != null ? pair.Value.ToString() : String.Empty) + "\r\n";
                        result += line;
                    }
                }
            }

            return result;
        }

        private void SetTracebackOutput(string text)
        {
            if (this.m_onclosing)
                return;

            if (this.textBoxDebug.InvokeRequired)
            {
                SetTracebackDelegate d = new(SetTracebackOutput);
                this.Invoke(d, new object[] { text });
            }
            else
            {
                this.textBoxDebug.Text = text;
            }
        }

        private void SetErrorBox(string text)
        {
            if (this.m_onclosing)
                return;

            try
            {
                //if (this.messagelistBox.InvokeRequired)
                if (this.outputConsole.InvokeRequired)
                {
                    SetTracebackDelegate d = new(SetErrorBox);
                    this.Invoke(d, new object[] { text });
                }
                else
                {
                    var lines_txt = text.Trim('\n');
                    var multiline = lines_txt.IndexOf("\n") > 0;
                    var multiline_txt = multiline ? "\n" : " ";
                    var time_txt = "[" + DateTime.Now.ToString("HH:mm:ss") + "]";
                    var showTimestamp = timeToolStripMenuItem.Checked; // logboxMenuStrip.
                    var msg = showTimestamp ? time_txt + multiline_txt : "";
                    msg += lines_txt + '\n';

                    //this.messagelistBox.Items.Add(msg);
                    //this.messagelistBox.TopIndex = this.messagelistBox.Items.Count - 1;

                    outputConsole.AppendText(msg);
                    outputConsole.SelectionStart = outputConsole.Text.Length - 1;
                    outputConsole.ScrollToCaret();
                }
            }
            catch
            {
                // This is not right. UOS scripts
                // executed without a editor still have the m_writer defined but 
                //  There is no console so an exception is thrown
            }
        }

        private void EnhancedScriptEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            var shouldClose = OnUnload();
            e.Cancel = !shouldClose;
        }

        private void ToolStripButtonPlay_Click(object sender, EventArgs e)
        {
            Start(false);
        }

        private void ToolStripButtonDebug_Click(object sender, EventArgs e)
        {
            Start(true);
        }

        private void ToolStripNextCall_Click(object sender, EventArgs e)
        {
            EnqueueCommand(Command.Call);
        }

        private void ToolStripButtonNextLine_Click(object sender, EventArgs e)
        {
            EnqueueCommand(Command.Line);
        }

        private void ToolStripButtonNextReturn_Click(object sender, EventArgs e)
        {
            EnqueueCommand(Command.Return);
        }

        private void ToolStripButtonNextBreakpoint_Click(object sender, EventArgs e)
        {
            EnqueueCommand(Command.Breakpoint);
        }

        private void ToolStripButtonStop_Click(object sender, EventArgs e)
        {
            Stop();
        }

        private void ToolStripButtonAddBreakpoint_Click(object sender, EventArgs e)
        {
            AddBreakpoint();
        }

        private void ToolStripButtonRemoveBreakpoints_Click(object sender, EventArgs e)
        {
            RemoveBreakpoint();
        }

        private void ToolStripButtonOpen_Click(object sender, EventArgs e)
        {
            Open();
        }

        private void ToolStripButtonSave_Click(object sender, EventArgs e)
        {
            Save();
        }

        private void ToolStripButtonFormat_Click(object sender, EventArgs e)
        {
            FormatDocument();
        }

        private void ToolStripButtonUndoFormat_Click(object sender, EventArgs e)
        {
            // 撤销格式化：格式化通过 InsertText 写入，撤销一次即可还原
            fastColoredTextBoxEditor.Undo();
            SetErrorBox("已撤销上一步(可继续 Ctrl+Z 撤销更多)");
        }

        private void ToolStripButtonSaveAs_Click(object sender, EventArgs e)
        {
            SaveAs();
        }

        private void ToolStripButtonClose_Click(object sender, EventArgs e)
        {
            var language = m_Script.Language;
            CloseAndSave();
            LoadNewFile(language);
        }

        private void ToolStripButtonInspect_Click(object sender, EventArgs e)
        {
            InspectEntities();
        }

        private void ToolStripInspectGump_Click(object sender, EventArgs e)
        {
            InspectGumps();
        }

        private void ToolStripRecord_Click(object sender, EventArgs e)
        {
            ScriptRecord();
        }

        private static void Gumpinspector_close(object sender, EventArgs e)
        {
            Assistant.Engine.MainWindow.GumpInspectorEnable = false;
        }

        private void ToolStripButtonSearch_Click(object sender, EventArgs e)
        {
            fastColoredTextBoxEditor.Focus();
            SendKeys.SendWait("^f");
        }
        private void ToolStripButtonWiki_Click(object sender, EventArgs e)
        {
            System.Diagnostics.ProcessStartInfo p = new("http://www.razorenhanced.net/dokuwiki");
            try
            {
                System.Diagnostics.Process.Start(p);
            }
            catch { }
        }
        private void Open()
        {


            OpenFileDialog open = new()
            {
                Filter = "Script Files|*.py;*.txt;*.uos;*.cs",
                RestoreDirectory = true
            };
            if (open.ShowDialog() == DialogResult.OK)
            {
                if (open.FileName != null && File.Exists(open.FileName))
                {
                    LoadFromFile(open.FileName);
                }
            }
        }

        private void ReloadAfterSave()
        {

            /*
            EnhancedScript script = Scripts.Search(m_Script.Filename);
            if (script != null)
            {
                string fullpath = Path.Combine(Assistant.Engine.RootPath, "Scripts", m_Script.Filename);

                if (File.Exists(fullpath) && Scripts.EnhancedScripts.ContainsKey(m_Script.Filename))
                {
                    //string text = File.ReadAllText(fullpath);
                    //bool loop = script.Loop;
                    //bool wait = script.Wait;
                    //bool run = script.Run;
                    //bool autostart = script.AutoStart;
                    bool isRunning = script.IsRunning;

                    if (isRunning)
                        script.Stop();

                    //Scripts.EnhancedScript reloaded = new Scripts.EnhancedScript(m_Filename, text, wait, loop, run, autostart);
                    //reloaded.Create(null);
                    Scripts.EnhancedScripts[m_Script.Filename].LastModified = DateTime.MinValue;

                    if (isRunning)
                        script.Start();
                }
            }
            */
        }


        private void Save()
        {
            m_Script.Text = fastColoredTextBoxEditor.Text;
            if (m_Script.Exist)
            {
                UpdateTitle();
                m_Script.Save();
            }
            else
            {
                SaveAs();
            }
        }

        private void SaveAs()
        {
            m_Script.Text = fastColoredTextBoxEditor.Text;

            var language = m_Script.GetLanguage();
            string filter;
            switch (language)
            {
                default:
                case ScriptLanguage.PYTHON:
                    filter = "Python Files|*.py|Text Files|*.txt"; break;
                case ScriptLanguage.CSHARP:
                    filter = "C# Files|*.cs|Text Files|*.txt"; break;
                case ScriptLanguage.UOSTEAM:
                    filter = "UOS Files|*.uos|Text Files|*.txt"; break;
            }


            SaveFileDialog save = new()
            {
                Filter = filter,
                RestoreDirectory = true
            };

            if (m_Script.HasValidPath && !m_Script.Exist)
            {
                save.InitialDirectory = Path.GetDirectoryName(m_Script.Fullpath);
                save.FileName = m_Script.Filename;
            }
            else
            {
                save.InitialDirectory = Path.Combine(Assistant.Engine.RootPath, "Scripts");
            }

            if (save.ShowDialog() == DialogResult.OK)
            {
                m_Script.Stop();
                var fullpath = save.FileName;

                if (m_Script.Editor)
                {
                    EnhancedScript.Service.RemoveScript(m_Script);
                }

                if (File.Exists(fullpath))
                {
                    m_Script = EnhancedScript.FromFile(fullpath, editor: true);
                    m_Script.Text = fastColoredTextBoxEditor.Text;
                }
                else
                {
                    m_Script.Fullpath = fullpath;
                }


                m_Script.Save();


                UpdateTitle();
            }
        }

        private static readonly Regex WhitespaceRegex = new Regex(@"\s+", RegexOptions.Compiled);

        static bool CompareIgnoringWhitespace(string str1, string str2)
        {
            return WhitespaceRegex.Replace(str1, "") ==
                   WhitespaceRegex.Replace(str2, "");
        }

        private bool CloseAndSave()
        {
            m_onclosing = true;
            Stop();
            if (m_Recorder != null) { m_Recorder.Stop(); }

            // Not ask to save empty text
            var editorContent = fastColoredTextBoxEditor.Text;
            if (fastColoredTextBoxEditor.IsChanged && (editorContent != null || editorContent != ""))
            {
                string fileContent = "";
                if (m_Script.Exist)
                {
                    try
                    {
                        fileContent = File.ReadAllText(m_Script.Fullpath);
                    }
                    catch { }
                }
                if (!CompareIgnoringWhitespace(fileContent, editorContent))
                {
                    var dialogResult = RazorEnhanced.UI.RE_MessageBox.Show("Save current file?",
                        $"Save file named:\r\n{m_Script.Fullpath}",
                        ok: "Yes", no: "No", cancel: "Cancel", backColor: null);
                    if (dialogResult == DialogResult.Cancel)
                    {
                        return false;
                    }
                    if (dialogResult == DialogResult.No)
                    {
                        if (m_Script.Exist)
                        {
                            m_Script.Load(true);
                        }
                        else
                        {
                            UnloadScript();
                        }
                        return true;
                    }
                    if (dialogResult == DialogResult.Yes)
                    {

                        if (m_Script.HasValidPath)
                        {
                            Save();
                        }
                        else
                        {
                            SaveFileDialog save = new()
                            {
                                Filter = "Script Files|*.py|Script Files|*.txt|C# Files|*.cs",
                                FileName = m_Script.Fullpath
                            };

                            if (save.ShowDialog() == DialogResult.OK)
                            {
                                if (save.FileName != null && save.FileName != string.Empty)
                                {
                                    m_Script.Fullpath = save.FileName;
                                    Save();
                                }
                            }
                            else
                            {
                                return false;
                            }
                        }

                    }
                }

            }
            UnloadScript();
            m_onclosing = false;
            return true;
        }

        public void UnloadScript()
        {
            fastColoredTextBoxEditor.Text = String.Empty;
            if (m_Script.Editor)
            {
                EnhancedScript.Service.RemoveScript(m_Script);
            }
            m_Script = null;
            UpdateTitle();
        }


        private void AddBreakpoint()
        {
            int iline = fastColoredTextBoxEditor.Selection.Start.iLine;

            if (!m_Breakpoints.Contains(iline))
            {
                m_Breakpoints.Add(iline + 1);
                FastColoredTextBoxNS.Line line = fastColoredTextBoxEditor[iline];
                line.BackgroundBrush = new SolidBrush(Color.Red);
                fastColoredTextBoxEditor.Invalidate();
            }
        }

        private void RemoveBreakpoint()
        {
            int iline = fastColoredTextBoxEditor.Selection.Start.iLine;

            if (m_Breakpoints.Contains(iline + 1))
            {
                m_Breakpoints.Remove(iline + 1);
                FastColoredTextBoxNS.Line line = fastColoredTextBoxEditor[iline];
                line.BackgroundBrush = new SolidBrush(Color.White);
                fastColoredTextBoxEditor.Invalidate();
            }
        }

        private void InspectEntities()
        {
            Targeting.OneTimeTarget(true, new Targeting.TargetResponseCallback(Commands.GetInfoTarget_Callback));
        }

        internal static void InspectGumps()
        {
            foreach (Form f in System.Windows.Forms.Application.OpenForms)
            {
                if (f is EnhancedGumpInspector af)
                {
                    af.Focus();
                    return;
                }
            }
            EnhancedGumpInspector ginspector = new();
            ginspector.FormClosed += new FormClosedEventHandler(Gumpinspector_close);
            ginspector.TopMost = true;
            ginspector.Show();
        }

        private void ScriptRecord()
        {
            if (!m_Script.IsRunning)
            {
                if (m_Recorder == null)
                {
                    m_Recorder = ScriptRecorderService.RecorderForLanguage(m_Script.Language);
                    m_Recorder.Output = (code) =>
                    {
                        fastColoredTextBoxEditor.Text += "\n" + code;
                    };
                }


                if (!m_Recorder.IsRecording())
                {
                    m_Recorder.Start();
                    //ScriptRecorder.OnRecord = true;
                    SetErrorBox("RECORDER: Start Record");
                    SetStatusLabel("ON RECORD", Color.Red);
                    SetRecordButton("Stop Record");
                    return;
                }
                else
                {
                    m_Recorder.Stop();
                    //ScriptRecorder.OnRecord = false;
                    SetErrorBox("RECORDER: Stop Record");
                    SetStatusLabel("IDLE", Color.DarkTurquoise);
                    SetRecordButton("Record");
                    return;
                }
            }
            else
            {
                SetErrorBox("RECORDER ERROR: Can't Record if script is running");
            }
        }

        // 手动格式化/规范化当前脚本(快捷键 Ctrl+Shift+F)。目前支持 Python：
        // 去行尾空格、Tab转4空格、压缩多余空行、补文件末尾换行、逗号/括号/运算符按 PEP8 规范空格。
        // 仅改动“代码区”，绝不修改字符串与注释内容；可用 Ctrl+Z 撤销。
        private void FormatDocument()
        {
            if (m_Script == null) return;
            if (m_Script.GetLanguage() != ScriptLanguage.PYTHON)
            {
                SetErrorBox("格式化目前仅支持 Python 脚本");
                return;
            }
            try
            {
                string original = fastColoredTextBoxEditor.Text;
                string formatted = PyCodeFormatter.Format(original);
                if (formatted == original)
                {
                    SetErrorBox("代码已是规范格式，无需改动");
                    return;
                }
                int caretLine = fastColoredTextBoxEditor.Selection.Start.iLine;
                fastColoredTextBoxEditor.SelectAll();
                fastColoredTextBoxEditor.InsertText(formatted);   // 走 InsertText 以支持撤销
                int line = Math.Min(caretLine, Math.Max(0, fastColoredTextBoxEditor.LinesCount - 1));
                fastColoredTextBoxEditor.Selection.Start = new FastColoredTextBoxNS.Place(0, line);
                fastColoredTextBoxEditor.DoCaretVisible();
                SetErrorBox("已按 Python 规范格式化");
            }
            catch (Exception ex)
            {
                SetErrorBox("格式化失败: " + ex.Message);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                //Open File
                case (Keys.Control | Keys.O):
                    Open();
                    return true;

                //Save File
                case (Keys.Control | Keys.S):
                    Save();
                    return true;

                //Save As File
                case (Keys.Control | Keys.Shift | Keys.S):
                    SaveAs();
                    return true;

                //Close File
                case (Keys.Control | Keys.E):
                    toolStripButtonClose.PerformClick();
                    return true;

                //Inspect Entities
                case (Keys.Control | Keys.I):
                    InspectEntities();
                    return true;

                //Inspect Gumps
                case (Keys.Control | Keys.G):
                    InspectGumps();
                    return true;

                case (Keys.Control | Keys.R):
                    ScriptRecord();
                    return true;

                //Format / 规范化当前脚本(Ctrl+Shift+F)
                case (Keys.Control | Keys.Shift | Keys.F):
                    FormatDocument();
                    return true;

                //Start with Debug
                case (Keys.F5):
                    Start(true);
                    return true;

                //Start without Debug
                case (Keys.F6):
                    Start(false);
                    return true;

                //Stop
                case (Keys.F4):
                    Stop();
                    return true;

                //Add Breakpoint
                case (Keys.F7):
                    AddBreakpoint();
                    return true;

                //Remove Breakpoint
                case (Keys.F8):
                    RemoveBreakpoint();
                    return true;

                //Next Breakpoint
                case (Keys.F9):
                    EnqueueCommand(Command.Breakpoint);
                    return true;

                //Debug - Next Line
                case (Keys.F10):
                    EnqueueCommand(Command.Line);
                    return true;

                //Debug - Next Call
                case (Keys.F11):
                    EnqueueCommand(Command.Call);
                    return true;

                //Debug - Next Return
                case (Keys.F12):
                    EnqueueCommand(Command.Return);
                    return true;

                default:
                    return base.ProcessCmdKey(ref msg, keyData);
            }
        }


        private void CopyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            fastColoredTextBoxEditor.Copy();
        }

        private void PasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            fastColoredTextBoxEditor.Paste();
        }

        private void CutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            fastColoredTextBoxEditor.Cut();
        }

        private void CommentSelectLineToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(fastColoredTextBoxEditor.SelectedText)) // No selection
                return;

            string[] lines = fastColoredTextBoxEditor.SelectedText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            fastColoredTextBoxEditor.SelectedText = "";
            for (int i = 0; i < lines.Count(); i++)
            {
                fastColoredTextBoxEditor.SelectedText += "#" + lines[i];
                if (i < lines.Count() - 1)
                    fastColoredTextBoxEditor.SelectedText += "\r\n";
            }
        }

        private IEnumerable<FastColoredTextBoxNS.Char> ConvertStringToCharEnumerable(string content)
        {
            // Convert each character in the string to a FastColoredTextBoxNS.Char
            return content.Select(c => new FastColoredTextBoxNS.Char(c));
        }

        // Common code to get type infor for a serial
        private ScriptRecorder.UsedObjectData? FindSerialTypeColor(string textSerial)
        {
            Int32 serial = 0;
            if (textSerial.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                string hexString = textSerial.Substring(2);
                serial = Convert.ToInt32(hexString, 16);
            }
            else
            {
                serial = Convert.ToInt32(textSerial, 10);
            }

            // Try to find existing item
            Item item = Items.FindBySerial(serial);
            if (item != null)
            {
                int container = item.Container;
                if (container == 0)
                    container = -1;
                return new ScriptRecorder.UsedObjectData((uint)serial, container, (ushort)item.ItemID, item.Hue);
            }

            // Maybe its a Mobil
            Mobile mobile = Mobiles.FindBySerial(serial);
            if (mobile != null)
                return new ScriptRecorder.UsedObjectData((uint)serial, -1, (ushort)mobile.Body, mobile.Hue);

            // Check for recently used (maybe and gone) items
            foreach (var entry in ScriptRecorder.RecentSerialType)
            {
                if (entry.serial == serial)
                    return entry;
            }

            // finally nothing found
            return null;
        }

        private void ConvertToByIdToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var currentLine = fastColoredTextBoxEditor.Selection.FromLine;
            var cursorLine = fastColoredTextBoxEditor.Selection.Start.iLine;
            var lineToChange = fastColoredTextBoxEditor.Lines[currentLine];
            var language = fastColoredTextBoxEditor.Language;
            switch (language)
            {
                case FastColoredTextBoxNS.Language.Python:
                    {
                        string pattern = @"(.*Items\.)\s*(\w+)\(\s*(\w+)\s*\)(.*$)";
                        Regex r = new(pattern, RegexOptions.IgnoreCase);
                        Match m = r.Match(lineToChange);
                        if (m.Success && m.Groups.Count == 5)
                        {
                            string frontPart = m.Groups[1].Value;
                            string useItem = m.Groups[2].Value;

                            string textSerial = m.Groups[3].Value;
                            string lastPart = m.Groups[4].Value;
                            if (useItem.lower() == "useitem")
                            {
                                var result = FindSerialTypeColor(textSerial);
                                if (result.HasValue)
                                {
                                    var objectData = result.Value;
                                    fastColoredTextBoxEditor.BeginUpdate();
                                    string newLine = $"{frontPart}UseItemByID(0x{objectData.itemId:x}, {objectData.hue} ){lastPart}";
                                    fastColoredTextBoxEditor.TextSource[currentLine].Clear();
                                    fastColoredTextBoxEditor.TextSource[currentLine].AddRange(ConvertStringToCharEnumerable(newLine));
                                    fastColoredTextBoxEditor.EndUpdate();
                                    fastColoredTextBoxEditor.Invalidate();
                                }

                            }
                        }
                        pattern = @"(.*Target\.)\s*(\w+)\(\s*(\w+)\s*\)(.*$)";
                        r = new Regex(pattern, RegexOptions.IgnoreCase);
                        m = r.Match(lineToChange);
                        if (m.Success && m.Groups.Count == 5)
                        {
                            string frontPart = m.Groups[1].Value;
                            string targetExecute = m.Groups[2].Value;

                            string textSerial = m.Groups[3].Value;
                            string lastPart = m.Groups[4].Value;
                            if (targetExecute.lower() == "targetexecute")
                            {
                                var result = FindSerialTypeColor(textSerial);
                                if (result.HasValue)
                                {
                                    var objectData = result.Value;
                                    fastColoredTextBoxEditor.BeginUpdate();
                                    string newLine = $"{frontPart}TargetType(0x{objectData.itemId:x}, {objectData.hue} ){lastPart}";
                                    fastColoredTextBoxEditor.TextSource[currentLine].Clear();
                                    fastColoredTextBoxEditor.TextSource[currentLine].AddRange(ConvertStringToCharEnumerable(newLine));
                                    fastColoredTextBoxEditor.EndUpdate();
                                    fastColoredTextBoxEditor.Invalidate();
                                }

                            }
                        }

                        break;
                    }

                case FastColoredTextBoxNS.Language.Uos:
                    {
                        string pattern = @"(.*)useobject\s+(\w+)(.*$)";
                        Regex r = new(pattern, RegexOptions.IgnoreCase);
                        Match m = r.Match(lineToChange);
                        if (m.Success && m.Groups.Count == 4)
                        {
                            string frontPart = m.Groups[1].Value;
                            string textSerial = m.Groups[2].Value;
                            string lastPart = m.Groups[3].Value;

                            var result = FindSerialTypeColor(textSerial);
                            if (result.HasValue)
                            {
                                var objectData = result.Value;
                                fastColoredTextBoxEditor.BeginUpdate();
                                string newLine = $"{frontPart}usetype 0x{objectData.itemId:x} {objectData.hue} {objectData.container}{lastPart}";
                                fastColoredTextBoxEditor.TextSource[currentLine].Clear();
                                fastColoredTextBoxEditor.TextSource[currentLine].AddRange(ConvertStringToCharEnumerable(newLine));
                                fastColoredTextBoxEditor.EndUpdate();
                                fastColoredTextBoxEditor.Invalidate();
                            }

                        }
                        pattern = @"(.*)target\s+(\w+)(.*$)";
                        r = new Regex(pattern, RegexOptions.IgnoreCase);
                        m = r.Match(lineToChange);
                        if (m.Success && m.Groups.Count == 4)
                        {
                            string frontPart = m.Groups[1].Value;
                            string textSerial = m.Groups[2].Value;
                            string lastPart = m.Groups[3].Value;

                            var result = FindSerialTypeColor(textSerial);
                            if (result.HasValue)
                            {
                                var objectData = result.Value;
                                fastColoredTextBoxEditor.BeginUpdate();
                                string newLine = $"{frontPart}targettype 0x{objectData.itemId:x} {objectData.hue} {objectData.container}{lastPart}";
                                fastColoredTextBoxEditor.TextSource[currentLine].Clear();
                                fastColoredTextBoxEditor.TextSource[currentLine].AddRange(ConvertStringToCharEnumerable(newLine));
                                fastColoredTextBoxEditor.EndUpdate();
                                fastColoredTextBoxEditor.Invalidate();
                            }

                        }

                        break;
                    }
            }
        }


        private void UnCommentLineToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(fastColoredTextBoxEditor.SelectedText)) // No selection
                return;

            string[] lines = fastColoredTextBoxEditor.SelectedText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            fastColoredTextBoxEditor.SelectedText = "";
            for (int i = 0; i < lines.Count(); i++)
            {
                fastColoredTextBoxEditor.SelectedText += lines[i].TrimStart('#');
                if (i < lines.Count() - 1)
                    fastColoredTextBoxEditor.SelectedText += "\r\n";
            }
        }

        private void MessagelistBox_KeyUp(object sender, KeyEventArgs e)
        {
            //if (messagelistBox.SelectedItems == null) return;
            if (outputConsole.SelectedText == "") return;  // Nothing selected

            if (e.Control && e.KeyCode == Keys.C)
            {
                Utility.ClipBoardCopy(outputConsole.SelectedText);
                //Utility.ClipBoardCopy(String.Join(Environment.NewLine, messagelistBox.SelectedItems.Cast<string>()));
            }
        }

        private void ClearToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //messagelistBox.Items.Clear();
            outputConsole.Text = "";
        }

        private void CopyToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            //if (messagelistBox.SelectedItems == null) return; 


            if (outputConsole.SelectedText == "") return;  // Nothing selected
            Utility.ClipBoardCopy(outputConsole.SelectedText);

        }

        private void ToolStripInspectAlias_Click(object sender, EventArgs e)
        {
            foreach (Form f in System.Windows.Forms.Application.OpenForms)
            {
                if (f is EnhancedObjectInspector af)
                {
                    af.Focus();
                    return;
                }
            }
            new EnhancedObjectInspector().Show();
        }

        private void EnhancedScriptEditor_Load(object sender, EventArgs e)
        {
            OnLoad();
        }

        private void timeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            timeToolStripMenuItem.Checked = !timeToolStripMenuItem.Checked;
        }

        private void autoclearToolStripMenuItem_Click(object sender, EventArgs e)
        {
            autoclearToolStripMenuItem.Checked = !autoclearToolStripMenuItem.Checked;
        }
    }

    public class ToolTipDescriptions
    {
        public string Title;
        public string[] Parameters;
        public string Returns;
        public string Description;
        public string Notes;

        public ToolTipDescriptions(string title, string[] parameter, string returns, string description, string notes = "")
        {
            Title = title;
            Parameters = parameter;
            Returns = returns;
            Description = description;
            Notes = notes;

        }

        public string ToolTipDescription()
        {
            string complete_description = "";

            //Description
            if (Description.Trim().Length > 0)
            {
                complete_description += Description.Trim() + "\n";
            }

            //Parameters
            complete_description += "\nParameters: ";
            if (Parameters.Length > 0)
            {
                complete_description += "\n" + String.Join("\n", Parameters.Select(text => "- " + text));
            }
            else
            {
                complete_description += "None";
            }
            complete_description += "\n";

            //Return
            complete_description += $"\nReturns: {Returns}";

            //Notes
            if (Notes.Length > 0)
            {
                complete_description += "\n---\n" + Notes;
            }
            return complete_description;
        }
    }



    #region Custom Items per Autocomplete

    /// <summary>
    /// This autocomplete item appears after dot
    /// </summary>
    public class MethodAutocompleteItemAdvance : MethodAutocompleteItem
    {
        readonly string firstPart;
        readonly string lastPart;

        public MethodAutocompleteItemAdvance(string text)
            : base(text)
        {
            var i = text.LastIndexOf('.');
            if (i < 0)
                firstPart = text;
            else
            {
                firstPart = text.Substring(0, i);
                lastPart = text.Substring(i + 1);
            }
        }

        public override CompareResult Compare(string fragmentText)
        {
            int i = fragmentText.LastIndexOf('.');

            if (i < 0)
            {
                if (firstPart.StartsWith(fragmentText) && string.IsNullOrEmpty(lastPart))
                    return CompareResult.VisibleAndSelected;
                //if (firstPart.ToLower().Contains(fragmentText.ToLower()))
                //  return CompareResult.Visible;
            }
            else
            {
                var fragmentFirstPart = fragmentText.Substring(0, i);
                var fragmentLastPart = fragmentText.Substring(i + 1);


                if (firstPart != fragmentFirstPart)
                    return CompareResult.Hidden;

                if (lastPart != null && lastPart.StartsWith(fragmentLastPart))
                    return CompareResult.VisibleAndSelected;

                if (lastPart != null && lastPart.ToLower().Contains(fragmentLastPart.ToLower()))
                    return CompareResult.Visible;

            }

            return CompareResult.Hidden;
        }

        public override string GetTextForReplace()
        {
            if (lastPart == null)
                return firstPart;

            return firstPart + "." + lastPart;
        }

        public override string ToString()
        {
            if (lastPart == null)
                return firstPart;

            return lastPart;
        }
    }

    /// <summary>
    /// This autocomplete item appears after dot
    /// </summary>
    public class SubPropertiesAutocompleteItem : MethodAutocompleteItem
    {
        readonly string firstPart;
        readonly string lastPart;

        public SubPropertiesAutocompleteItem(string text)
            : base(text)
        {
            var i = text.LastIndexOf('.');
            if (i < 0)
                firstPart = text;
            else
            {
                var keywords = text.Split('.');
                if (keywords.Length >= 2)
                {
                    firstPart = keywords[keywords.Length - 2];
                    lastPart = keywords[keywords.Length - 1];
                }
                else
                {
                    firstPart = text.Substring(0, i);
                    lastPart = text.Substring(i + 1, text.Length);
                }
            }
        }

        public override CompareResult Compare(string fragmentText)
        {
            int i = fragmentText.LastIndexOf('.');

            if (i < 0)
            {
                if (firstPart.StartsWith(fragmentText) && string.IsNullOrEmpty(lastPart))
                    return CompareResult.VisibleAndSelected;
                //if (firstPart.ToLower().Contains(fragmentText.ToLower()))
                //  return CompareResult.Visible;
            }
            else
            {
                var keywords = fragmentText.Split('.');
                if (keywords.Length >= 2)
                {
                    var fragmentFirstPart = keywords[keywords.Length - 2];
                    var fragmentLastPart = keywords[keywords.Length - 1];


                    if (firstPart != fragmentFirstPart)
                        return CompareResult.Hidden;

                    if (lastPart != null && lastPart.StartsWith(fragmentLastPart))
                        return CompareResult.VisibleAndSelected;

                    if (lastPart != null && lastPart.ToLower().Contains(fragmentLastPart.ToLower()))
                        return CompareResult.Visible;
                }
                else
                {
                    var fragmentFirstPart = fragmentText.Substring(0, i);
                    var fragmentLastPart = fragmentText.Substring(i + 1);


                    if (firstPart != fragmentFirstPart)
                        return CompareResult.Hidden;

                    if (lastPart != null && lastPart.StartsWith(fragmentLastPart))
                        return CompareResult.VisibleAndSelected;

                    if (lastPart != null && lastPart.ToLower().Contains(fragmentLastPart.ToLower()))
                        return CompareResult.Visible;
                }

            }

            return CompareResult.Hidden;
        }

        public override string GetTextForReplace()
        {
            if (lastPart == null)
                return firstPart;

            return firstPart + "." + lastPart;
        }

        public override string ToString()
        {
            if (lastPart == null)
                return firstPart;

            return lastPart;
        }
    }

    #endregion

    /// <summary>
    /// 轻量 Python 代码格式化器（零依赖）。
    /// 安全规则：去行尾空格、Tab→4空格、压缩多余空行(≤2)、补文件末尾换行；
    /// 在“代码区”按 PEP8 规范空格（逗号后空格、括号内紧贴、二元运算符两侧空格、
    /// 一元/解包运算符紧贴操作数、关键字参数 = 紧贴等）。
    /// 字符串(含三引号/前缀 f r b u)与注释内容【原样保留，绝不修改】。
    /// </summary>
    internal static class PyCodeFormatter
    {
        private enum K { Word, Num, Str, Comment, BinOp, UnOp, TightEq, LParen, LBrack, LBrace, Close, Comma, Colon, Semi, Dot, Other }

        private struct Tok { public string Text; public K Kind; }

        // 期待值的关键字（其后的 +/-/* 等应视为一元/起始，而非二元）
        private static readonly HashSet<string> ValueKeywords = new HashSet<string>
        {
            "return","yield","and","or","not","in","is","if","elif","else","while",
            "assert","lambda","del","raise","from","with","await","print","exec"
        };

        // 多字符运算符（按长度优先匹配）
        private static readonly string[] Operators =
        {
            "**=","//=",">>=","<<=","==","!=","<=",">=","->",":=",
            "+=","-=","*=","/=","%=","&=","|=","^=","**","//","<<",">>",
            "+","-","*","/","%","<",">","=","&","|","^","~","@"
        };

        // 总是二元的运算符（不会作一元）
        private static readonly HashSet<string> AlwaysBinary = new HashSet<string>
        {
            "==","!=","<=",">=","->",":=","+=","-=","*=","/=","%=","&=","|=","^=",
            "**=","//=",">>=","<<=","<<",">>","<",">","&","|","^"
        };

        public static string Format(string code)
        {
            if (string.IsNullOrEmpty(code)) return code ?? "";
            code = code.Replace("\r\n", "\n").Replace("\r", "\n");
            string[] raw = code.Split('\n');

            var outLines = new List<string>(raw.Length);
            bool inTriple = false;
            char tripleChar = '"';
            int depth = 0;

            foreach (string rawLine in raw)
            {
                if (inTriple)
                {
                    // 处于跨行三引号字符串内部：整行原样保留，仅检测是否在本行闭合
                    outLines.Add(TrimEndKeepNL(rawLine));
                    if (rawLine.IndexOf(new string(tripleChar, 3), StringComparison.Ordinal) >= 0)
                        inTriple = false;
                    continue;
                }

                // 取出前导缩进，Tab → 4 空格
                int i = 0;
                var indent = new System.Text.StringBuilder();
                while (i < rawLine.Length && (rawLine[i] == ' ' || rawLine[i] == '\t'))
                {
                    indent.Append(rawLine[i] == '\t' ? "    " : " ");
                    i++;
                }
                string rest = rawLine.Substring(i);

                List<Tok> toks = Tokenize(rest, ref depth, ref inTriple, ref tripleChar);
                string body = Emit(toks);
                string line = (indent.ToString() + body);
                // 去行尾空格（保留空行为长度0）
                line = line.TrimEnd();
                outLines.Add(line);
            }

            // 压缩 3 行以上连续空行为最多 2 行
            var collapsed = new List<string>(outLines.Count);
            int blanks = 0;
            foreach (var l in outLines)
            {
                if (l.Length == 0)
                {
                    blanks++;
                    if (blanks <= 2) collapsed.Add(l);
                }
                else { blanks = 0; collapsed.Add(l); }
            }
            // 去掉首尾多余空行
            while (collapsed.Count > 0 && collapsed[0].Length == 0) collapsed.RemoveAt(0);
            while (collapsed.Count > 0 && collapsed[collapsed.Count - 1].Length == 0) collapsed.RemoveAt(collapsed.Count - 1);

            return string.Join("\n", collapsed) + "\n";
        }

        private static string TrimEndKeepNL(string s) => s.TrimEnd();

        // 把一行(已去前导缩进)切成 token；字符串/注释作为不可改的整体 token
        private static List<Tok> Tokenize(string s, ref int depth, ref bool inTriple, ref char tripleChar)
        {
            var toks = new List<Tok>();
            int n = s.Length, i = 0;
            while (i < n)
            {
                char c = s[i];

                if (c == ' ' || c == '\t') { i++; continue; }

                // 注释：到行尾
                if (c == '#')
                {
                    string com = s.Substring(i);
                    // # 后补一个空格(不含 shebang #!)，注释正文不改
                    if (com.Length > 1 && com[1] != ' ' && com[1] != '!' && com[1] != '#')
                        com = "# " + com.Substring(1);
                    toks.Add(new Tok { Text = com, Kind = K.Comment });
                    break;
                }

                // 字符串前缀 (f/r/b/u 及组合) + 引号
                if ((c == 'f' || c == 'F' || c == 'r' || c == 'R' || c == 'b' || c == 'B' || c == 'u' || c == 'U'))
                {
                    int j = i;
                    while (j < n && "fFrRbBuU".IndexOf(s[j]) >= 0 && (j - i) < 2) j++;
                    if (j < n && (s[j] == '"' || s[j] == '\'') && (j - i) >= 1)
                    {
                        string prefix = s.Substring(i, j - i);
                        string lit = ScanString(s, ref j, ref inTriple, ref tripleChar);
                        toks.Add(new Tok { Text = prefix + lit, Kind = K.Str });
                        i = j;
                        if (inTriple) break;
                        continue;
                    }
                }

                // 普通字符串
                if (c == '"' || c == '\'')
                {
                    int j = i;
                    string lit = ScanString(s, ref j, ref inTriple, ref tripleChar);
                    toks.Add(new Tok { Text = lit, Kind = K.Str });
                    i = j;
                    if (inTriple) break;
                    continue;
                }

                // 数字（含 . 指数 0x 等），避免 1e-10 被拆开
                if (char.IsDigit(c) || (c == '.' && i + 1 < n && char.IsDigit(s[i + 1])))
                {
                    int j = i + 1;
                    while (j < n)
                    {
                        char d = s[j];
                        if (char.IsLetterOrDigit(d) || d == '.' || d == '_') { j++; }
                        else if ((d == '+' || d == '-') && j > 0 && (s[j - 1] == 'e' || s[j - 1] == 'E')) { j++; }
                        else break;
                    }
                    toks.Add(new Tok { Text = s.Substring(i, j - i), Kind = K.Num });
                    i = j;
                    continue;
                }

                // 标识符/关键字
                if (char.IsLetter(c) || c == '_')
                {
                    int j = i + 1;
                    while (j < n && (char.IsLetterOrDigit(s[j]) || s[j] == '_')) j++;
                    toks.Add(new Tok { Text = s.Substring(i, j - i), Kind = K.Word });
                    i = j;
                    continue;
                }

                // 括号
                if (c == '(') { toks.Add(new Tok { Text = "(", Kind = K.LParen }); depth++; i++; continue; }
                if (c == '[') { toks.Add(new Tok { Text = "[", Kind = K.LBrack }); depth++; i++; continue; }
                if (c == '{') { toks.Add(new Tok { Text = "{", Kind = K.LBrace }); depth++; i++; continue; }
                if (c == ')' || c == ']' || c == '}') { toks.Add(new Tok { Text = c.ToString(), Kind = K.Close }); if (depth > 0) depth--; i++; continue; }

                if (c == ',') { toks.Add(new Tok { Text = ",", Kind = K.Comma }); i++; continue; }
                if (c == ':') { toks.Add(new Tok { Text = ":", Kind = K.Colon }); i++; continue; }
                if (c == ';') { toks.Add(new Tok { Text = ";", Kind = K.Semi }); i++; continue; }
                if (c == '.') { toks.Add(new Tok { Text = ".", Kind = K.Dot }); i++; continue; }

                // 运算符（最长匹配）
                string op = MatchOperator(s, i);
                if (op != null)
                {
                    Tok prev = toks.Count > 0 ? toks[toks.Count - 1] : default(Tok);
                    bool hasPrev = toks.Count > 0;
                    K k;
                    if (op == "~") k = K.UnOp;
                    else if (op == "=") k = depth > 0 ? K.TightEq : K.BinOp;
                    else if (AlwaysBinary.Contains(op)) k = K.BinOp;
                    else
                    {
                        // +,-,*,/,//,%,**,@ : 看前一个 token 是否“操作数”决定一元/二元
                        bool prevOperand = hasPrev && (prev.Kind == K.Close || prev.Kind == K.Num || prev.Kind == K.Str ||
                                            (prev.Kind == K.Word && !ValueKeywords.Contains(prev.Text)));
                        k = prevOperand ? K.BinOp : K.UnOp;
                    }
                    toks.Add(new Tok { Text = op, Kind = k });
                    i += op.Length;
                    continue;
                }

                // 其它单字符（如 \ 续行符、? 等）原样
                toks.Add(new Tok { Text = c.ToString(), Kind = K.Other });
                i++;
            }
            return toks;
        }

        // 扫描从 s[j] 处的引号字符串字面量，返回字面量文本(含引号)；j 推进到末尾之后。
        // 处理三引号跨行：未闭合则设置 inTriple。
        private static string ScanString(string s, ref int j, ref bool inTriple, ref char tripleChar)
        {
            int n = s.Length;
            char q = s[j];
            // 三引号
            if (j + 2 < n && s[j + 1] == q && s[j + 2] == q)
            {
                int start = j;
                int k = j + 3;
                string close = new string(q, 3);
                int idx = s.IndexOf(close, k, StringComparison.Ordinal);
                if (idx < 0)
                {
                    inTriple = true; tripleChar = q;
                    j = n;
                    return s.Substring(start); // 到行尾，后续行原样保留
                }
                j = idx + 3;
                return s.Substring(start, j - start);
            }
            // 单/双引号
            int st = j;
            j++;
            while (j < n)
            {
                if (s[j] == '\\') { j += 2; continue; }
                if (s[j] == q) { j++; break; }
                j++;
            }
            return s.Substring(st, Math.Min(j, n) - st);
        }

        private static string MatchOperator(string s, int i)
        {
            foreach (var op in Operators)
                if (i + op.Length <= s.Length && string.CompareOrdinal(s, i, op, 0, op.Length) == 0)
                    return op;
            return null;
        }

        private static bool IsOperand(K k) => k == K.Word || k == K.Num || k == K.Str || k == K.Close;

        private static string Emit(List<Tok> toks)
        {
            var sb = new System.Text.StringBuilder();
            for (int idx = 0; idx < toks.Count; idx++)
            {
                if (idx > 0) sb.Append(Sep(toks[idx - 1], toks[idx]));
                sb.Append(toks[idx].Text);
            }
            return sb.ToString();
        }

        // 决定两个相邻 token 之间是 "" 还是 " "
        private static string Sep(Tok prev, Tok cur)
        {
            K pk = prev.Kind, ck = cur.Kind;

            // 注释前：行首注释无空格(缩进已处理)，行内注释一个空格
            if (ck == K.Comment) return " ";

            // 这些之前不留空格
            if (ck == K.Comma || ck == K.Semi || ck == K.Colon || ck == K.Close || ck == K.Dot) return "";
            // 这些之后不留空格
            if (pk == K.LParen || pk == K.LBrack || pk == K.LBrace || pk == K.Dot) return "";
            // 一元/解包运算符紧贴右侧操作数
            if (pk == K.UnOp) return "";
            // 关键字参数 = 两侧紧贴
            if (pk == K.TightEq || ck == K.TightEq) return "";
            // 逗号后一个空格
            if (pk == K.Comma) return " ";
            // 冒号后保持紧贴(切片/字典/注解安全)
            if (pk == K.Colon) return "";
            // 左括号/下标：紧贴(函数调用/索引)否则空格
            if (ck == K.LParen || ck == K.LBrack)
                return (pk == K.Word || pk == K.Num || pk == K.Str || pk == K.Close) ? "" : " ";
            if (ck == K.LBrace) return " ";
            // 一元运算符前：紧跟开括号则不留空格(如 (-1)),否则一个空格(如 = -1)
            if (ck == K.UnOp)
                return (pk == K.LParen || pk == K.LBrack || pk == K.LBrace) ? "" : " ";
            // 二元运算符两侧空格
            if (ck == K.BinOp || pk == K.BinOp) return " ";
            // 两个操作数之间(关键字与值等)一个空格
            if (IsOperand(pk) && (ck == K.Word || ck == K.Num || ck == K.Str)) return " ";
            return " ";
        }
    }
}
