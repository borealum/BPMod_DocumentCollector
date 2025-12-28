using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using MelonLoader.Utils;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEngine;

using static BPMod_DocumentCollector.Mod_DocumentCollector;

[assembly: MelonInfo(typeof(BPMod_DocumentCollector.Mod_DocumentCollector), "BPMod_DocumentCollector", "1.0.0", "Borealum", null)]
[assembly: MelonGame("Dogubomb", "BLUE PRINCE")]

namespace BPMod_DocumentCollector
{
    public class Mod_DocumentCollector : MelonMod
    {
        //configuration stuff
        public static string loggerModName = "BPMod_DocumentCollector - ";
        public static String categoryID = "BPMod_DocumentCollector";
        private static MelonPreferences_Category category;
        String foundDocumentIDsID = "BPMod_DocumentCollector_foundDocs";
        private static MelonPreferences_Entry<List<String>> foundDocumentIDsList;
        public static HashSet<String> foundDocumentIDsSet;
        String keyBindingsID = "BPMod_DocumentCollector_keyBindings";
        private static MelonPreferences_Entry<string> keyBindingsJSON;
        public static KeyBindings keyBindings;

        String onlyInLibraryID = "BPMod_DocumentCollector_onlyInLibrary";
        public static MelonPreferences_Entry<bool> onlyInLibrary;
        String showAllDocsID = "BPMod_DocumentCollector_showAllDocs";
        public static MelonPreferences_Entry<bool> showAllDocs;
        String allwaysMagnifyID = "BPMod_DocumentCollector_allwaysMagnify";
        public static MelonPreferences_Entry<bool> allwaysMagnify;

        private string csvFilename = "documentsMetadata.csv";
        private static List<DocumentRecord> documentRecords;
        private static Dictionary<string, DocumentRecord> documentRecordsMap;
        string documentsPath = "UI OVERLAY CAM/UI Documents/DOCUMENTS";
        string uiDocumentsPath = "UI OVERLAY CAM/UI Documents";
        string turnButtonsPath = "UI OVERLAY CAM/UI Documents/Page Turn Buttons";
        string magnifyGlassPath = "UI OVERLAY CAM/UI Documents/MAG - ANchor/Cosimo Mag";
        string inventoryPath = "__SYSTEM/Inventory/Inventory (PickedUp)";
        string fpsControllerPath = "__SYSTEM/FPS Home/FPSController - Prince";
        string roomTextPath = "__SYSTEM/HUD/Room Text";

        public static GameObject uiDocumentsGO;
        public static GameObject turnButtonsGO;
        public static GameObject fpsControllerGO;
        public static GameObject roomTextGO;
        public static GameObject magnifyGlassGO;
        public static GameObject inventoryGO;

        //static Dictionary<String, GameObject> dummyGOsMap = new(); //these exist to replace realworld placed documents
        public static MenuTreeView menuTreeView = new MenuTreeView();

        public override void OnInitializeMelon()
        {
            category = MelonPreferences.CreateCategory(categoryID);
            foundDocumentIDsList = MelonPreferences.CreateEntry<List<string>>(categoryID, foundDocumentIDsID, new List<string>(), foundDocumentIDsID, "List of found documents");
            LoggerInstance.Msg($"{foundDocumentIDsID} size (in configuration) = {foundDocumentIDsList.Value.Count}");
            foundDocumentIDsSet = new HashSet<string>(foundDocumentIDsList.Value);

            keyBindingsJSON = MelonPreferences.CreateEntry<String>(categoryID, keyBindingsID, MiniJsonUtil.ToJson(new KeyBindings()), keyBindingsID, "Menu controls. Default = '{\"activateMenu\":\"L\",\"up\":\"UpArrow\",\"down\":\"DownArrow\",\"right\":\"RightArrow\",\"left\":\"LeftArrow\",\"select\":\"Return\",\"exit\":\"Escape\"}'");
            LoggerInstance.Msg($"{keyBindingsID} = {keyBindingsJSON.Value}");
            keyBindings = MiniJsonUtil.FromJson<KeyBindings>(keyBindingsJSON.Value);

            onlyInLibrary = MelonPreferences.CreateEntry<bool>(categoryID, onlyInLibraryID, true, onlyInLibraryID, "Menu only available in the Library. Default = true");
            LoggerInstance.Msg($"{onlyInLibraryID} = {onlyInLibrary.Value}");
            allwaysMagnify = MelonPreferences.CreateEntry<bool>(categoryID, allwaysMagnifyID, false, allwaysMagnifyID, "Allways activate magnifying glass when using the viewer menu. (Even when not picked up.) Default = false");
            LoggerInstance.Msg($"{allwaysMagnifyID} = {allwaysMagnify.Value}");
            showAllDocs = MelonPreferences.CreateEntry<bool>(categoryID, showAllDocsID, false, showAllDocsID, "Show all* existing in-game documents in the menu. Default = false");
            LoggerInstance.Msg($"{showAllDocsID} = {showAllDocs.Value}");

            string modFolder = Path.Combine(MelonEnvironment.UserDataDirectory, categoryID);
            string csvPath = Path.Combine(modFolder, csvFilename);

            documentRecordsMap = CSVReader.ReadCSV(csvPath);
            LoggerInstance.Msg($"found records in csv: {documentRecordsMap.Count}");
            documentRecords = documentRecordsMap
                       .OrderBy(kvp => kvp.Value.Category)
                       .ThenBy(kvp => kvp.Value.Name)
                       .Select(kvp => kvp.Value)
                       .ToList();
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (sceneName != null && sceneName.Equals("Mount Holly Estate"))
            {
                /*
                 //I used this just to generate a list for the.csv
                GameObject documentsList = GameObject.Find(documentsPath);
                if (documentsList != null)
                {
                    var transform = documentsList.transform;
                    for (int i = 0; i < transform.childCount; i++)
                    {
                        Transform child = transform.GetChild(i);
                        //LoggerInstance.Msg($"{child.name}");
                        foundDocumentIDs.Value.Add(child.name);
                    }
                }*/

                uiDocumentsGO = GameObject.Find(uiDocumentsPath);
                turnButtonsGO = GameObject.Find(turnButtonsPath);
                fpsControllerGO = GameObject.Find(fpsControllerPath);
                roomTextGO = GameObject.Find(roomTextPath);
                magnifyGlassGO = GameObject.Find(magnifyGlassPath);
                inventoryGO = GameObject.Find(inventoryPath);

                Dictionary<string, GameObject> uiDocumentGOsMap = new();
                GameObject uiDocument;
                //GameObject dummyParent;
                //GameObject dummyDocument;
                Transform parentT = GameObject.Find(documentsPath).transform;
                for (int i = 0; i < parentT.childCount; i++)
                {
                    uiDocument = parentT.GetChild(i).gameObject;
                    uiDocumentGOsMap.Add(uiDocument.name, uiDocument);
                }

                menuTreeView = new MenuTreeView();
                menuTreeView.rootNode = new MenuNode().WithLabel("Root").WithExpanded(true);
                //dummyParent = new GameObject("Dummy documents parent");
                foreach (DocumentRecord record in documentRecords)
                {
                    if (!record.Enabled || (!foundDocumentIDsSet.Contains(record.ID) && !showAllDocs.Value)) continue;
                    uiDocument = uiDocumentGOsMap[record.ID];
                    if (uiDocument == null)
                    {
                        LoggerInstance.Msg($"Didn't find game document with name {record.ID}");
                        continue;
                    }
                    //dummyDocument = new GameObject(record.ID);
                    //dummyDocument.transform.SetParent(dummyParent.transform);
                    //dummyGOsMap.Add(record.ID, childDocument);
                    //menuTreeView.AddRecord(record, uiDocument, dummyDocument);
                    menuTreeView.AddRecord(record, uiDocument);
                }
                LoggerInstance.Msg($"Created categories in menu: {menuTreeView.rootNode.Children.Count}");
            }
        }

        public override void OnUpdate()
        {
            menuTreeView.HandleInput();
        }

        public override void OnGUI()
        {
            menuTreeView.DrawTreeWindow();
        }

        //workaround for arrows not appearing on multipage documents after opening a 1 page document
        public static void EnableArrows()
        {
            turnButtonsGO.active = true;
            turnButtonsGO.GetComponent<PlayMakerFSM>().SendEvent("activate");
        }

        public static void DisableArrows()
        {
            turnButtonsGO.GetComponent<PlayMakerFSM>().SendEvent("deactivate");
            turnButtonsGO.active = false;
        }

        [HarmonyPatch(typeof(GameObject), "SetActive")]
        class Patch_SetActive
        {
            static void Postfix(GameObject __instance, bool value)
            {
                Transform parentT = __instance.transform.parent;
                if (parentT != null)
                {
                    string name = parentT.gameObject.name;
                    if (name.Equals("DOCUMENTS") && !menuTreeView.menuOn)//recording only happens when opening documents in the game world
                    {
                        if (value)
                        {
                            EnableArrows();
                            //saving opened documents to config when they are opened
                            bool exists = foundDocumentIDsSet.Contains(__instance.name);
                            if (exists)
                            {
                                return;
                            }
                            foundDocumentIDsSet.Add(__instance.name);
                            foundDocumentIDsList.Value.Add(__instance.name);
                            //add to tree hierarchy
                            DocumentRecord childRecord = documentRecordsMap[__instance.name];
                            if (childRecord == null)
                            {
                                MelonLogger.Msg(loggerModName + $"Couldn't find a record with ID {__instance.name} inside the metadata csv file. Found document was recorded, but won't be displayed in menu hierarchy.");
                                //TODO later - create record in csv file?
                            }
                            else if (!childRecord.Enabled)
                            {
                                MelonLogger.Msg(loggerModName + $"Document ID is set as disabled in csv file. Found document was recorded, but won't be displayed in menu hierarchy.");
                            }
                            else
                            {
                                menuTreeView.AddRecord(childRecord, __instance);
                            }
                            menuTreeView.menuDisabled = true;//disable showing menu when normal document is open to not break stuff
                        }
                        else
                        {
                            DisableArrows();
                            menuTreeView.menuDisabled = false;
                        }
                        //MelonLogger.Msg($"Patch_SetActive menuDisabled = {menuTreeView.menuDisabled} , instance = {__instance.name}");
                    }
                }
            }
        }
    }

    public static class CSVReader
    {
        public static Dictionary<string, DocumentRecord> ReadCSV(string csvPath)
        {
            Dictionary<string, DocumentRecord> data = new();
            var lines = File.ReadAllLines(csvPath);
            if (lines.Length < 2)
                return data;

            var headers = lines[0].Trim().Split(new char[] { ',', ';' });
            var columnIndex = new Dictionary<string, int>();
            for (int i = 0; i < headers.Length; i++)
            {
                columnIndex[headers[i].Trim()] = i;
            }
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;
                var cols = lines[i].Trim().Split(',');
                data.Add(cols[columnIndex["Name inGame"]], new DocumentRecord
                {
                    ID = cols[columnIndex["Name inGame"]],
                    Name = cols[columnIndex["Name forHumans"]],
                    Enabled = cols[columnIndex["Enabled"]] == "1",
                    Category = cols[columnIndex["Category"]]
                });
            }
            return data;
        }
    }

    public class DocumentRecord
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public bool Enabled { get; set; }
        public string Category { get; set; }
    }

    public class MenuNode
    {
        public string Label;
        public GameObject uiDocumentGO;
        public GameObject dummyDocumentGO;

        public MenuNode Parent;
        public List<MenuNode> Children = new();
        public int Depth;
        public bool Expanded = false;
        public bool IsLeaf => Children.Count == 0;
        public static MenuNode New() => new();

        public MenuNode()
        {
        }

        public MenuNode WithLabel(string label)
        {
            Label = label;
            return this;
        }

        public MenuNode WithDummyDocument(GameObject go)
        {
            dummyDocumentGO = go;
            return this;
        }

        public MenuNode WithUIDocument(GameObject go)
        {
            uiDocumentGO = go;
            return this;
        }

        public MenuNode WithExpanded(bool expanded)
        {
            Expanded = expanded;
            return this;
        }

        public MenuNode AddChild(MenuNode child)
        {
            child.Parent = this;
            Children.Add(child);
            child.Depth = Depth + 1;
            return this;
        }
    }

    public class MenuTreeView
    {
        //view
        public bool menuDisabled = false; //completely disable my custom inputs if opening a document normally to not break anything
        public bool menuOn = false;
        private int selectedIndex = 0;
        private GameObject openedBook;

        private const int rowHeight = 24;
        private const int textHeight = 20;
        private const int textPaddingTop = 0;
        private const int textPaddingBottom = 2;
        private const int indentWidth = 16;
        private GUIStyle labelStyle;
        private Rect windowRect = new Rect(20, 100, 400, 600);

        private Vector2 treeScroll;
        private float scrollInputInterval = 0.2f;
        private float nextInputTime = 0f;

        public MenuNode rootNode;
        public List<MenuNode> visibleNodes = new();
        Dictionary<String, MenuNode> categoryNodesMap = new();

        public MenuNode FindAddCategory(DocumentRecord documentRecord)
        {
            MenuNode categoryNode;
            if (!categoryNodesMap.TryGetValue(documentRecord.Category, out categoryNode))
            {
                categoryNode = new MenuNode().WithLabel(documentRecord.Category);
                rootNode.AddChild(categoryNode);
                categoryNodesMap.Add(documentRecord.Category, categoryNode);
            }
            return categoryNode;
        }

        public void AddRecord(DocumentRecord documentRecord, GameObject uiDocument)
        {
            MenuNode categoryNode = FindAddCategory(documentRecord);
            MenuNode childNode = new MenuNode().WithLabel(documentRecord.Name).WithUIDocument(uiDocument);
            categoryNode.AddChild(childNode);
        }

        public void BuildVisibleList(MenuNode node, List<MenuNode> result)
        {
            result.Add(node);
            if (!node.Expanded) return;
            foreach (var child in node.Children)
                BuildVisibleList(child, result);
        }

        public void DrawTreeWindow()
        {
            if (!menuOn) return;
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.alignment = TextAnchor.UpperLeft;
            labelStyle.fontSize = textHeight;
            labelStyle.padding.top = textPaddingTop;
            labelStyle.padding.bottom = textPaddingBottom;
            treeScroll.y = selectedIndex * rowHeight;
            windowRect.height = Math.Max(Screen.height - 200, 200);
            windowRect = GUI.Window(60001001, windowRect, (GUI.WindowFunction)DrawTree, "Library");
        }

        void DrawTree(int id)
        {
            treeScroll = GUILayout.BeginScrollView(treeScroll, false, true);
            visibleNodes.Clear();
            BuildVisibleList(rootNode, visibleNodes);
            for (int i = 0; i < visibleNodes.Count; i++)
            {
                var node = visibleNodes[i];
                var indent = node.Depth * indentWidth;
                var rect = GUILayoutUtility.GetRect(0, rowHeight, GUILayout.ExpandWidth(true));
                string suffix = "";
                if (i == selectedIndex)
                {
                    GUI.Box(rect, GUIContent.none);
                    suffix = " ●";
                }
                rect.x += indent;
                string prefix = node.Children.Count > 0 ? (node.Expanded ? "▼ " : "▶ ") : "  ";
                GUI.Label(rect, prefix + node.Label + suffix, labelStyle);
            }
            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        public void HandleInput()
        {
            //if (Input.anyKeyDown) {
            //    MelonLogger.Msg($"HandleInput menuDisabled = {menuDisabled}");
            //    MelonLogger.Msg($"HandleInput menuOn = {menuOn}");
            //    MelonLogger.Msg($"HandleInput openedBook = {openedBook}");
            //}

            if (!menuDisabled && openedBook == null)
            {
                if (Input.GetKeyDown(keyBindings.activateMenu))
                {
                    //very simple way to check current room (used in game for example for check when adding the hidden text to the Red Prince book (Master bedroom))
                    if (roomTextGO.GetComponent<PlayMakerFSM>().FsmVariables.FindFsmString("Current Room").Value.Equals("Library")
                        || !onlyInLibrary.Value)
                    {
                        menuOn = !menuOn;
                        //GameObject.Find(Mod_PortableLibrary.fpsCharPath)?.GetComponent<PlayMakerFSM>()?.SendEvent(menuOn ? "disable" : "enable");
                        fpsControllerGO.GetComponent<PlayMakerFSM>()?.SendEvent(menuOn ? "QuickFreeze" : "QuickUnfreeze");
                        return;
                    }
                }
                else if (Input.GetKeyDown(keyBindings.exit))
                {
                    menuOn = false;
                    fpsControllerGO.GetComponent<PlayMakerFSM>()?.SendEvent("QuickUnfreeze");
                    return;
                }
            }
            if (menuOn)
            {
                if (Input.GetKeyDown(keyBindings.select))
                {
                    OnNodeActivated(visibleNodes[selectedIndex]);
                }
                else if (Input.GetKeyDown(keyBindings.right))
                {
                    if (visibleNodes[selectedIndex].Expanded)
                        selectedIndex = selectedIndex + 1;
                    else
                        OnNodeActivated(visibleNodes[selectedIndex]);
                }
                else if (Input.GetKeyDown(keyBindings.left))
                {
                    OnNodeDeactivated(visibleNodes[selectedIndex]);
                }
                //inputs with interval delay
                if (Input.GetKey(keyBindings.down))
                {
                    if (Time.time >= nextInputTime)
                    {
                        nextInputTime = Time.time + scrollInputInterval;
                        if (selectedIndex < visibleNodes.Count - 1)
                        {
                            selectedIndex = selectedIndex + 1;
                        }
                    }
                }
                else if (Input.GetKey(keyBindings.up))
                {
                    if (Time.time >= nextInputTime)
                    {
                        nextInputTime = Time.time + scrollInputInterval;
                        if (selectedIndex > 0)
                        {
                            selectedIndex = selectedIndex - 1;
                        }
                    }
                }
                else
                {
                    nextInputTime = 0f;
                }
            }
            else //!menuOn
            {
                if (Input.GetKeyDown(keyBindings.exit) || Input.GetKeyDown(keyBindings.select))
                {
                    CloseBook();
                }
            }
        }

        private void OpenBook(MenuNode menuNode)
        {
            if (openedBook == null)
            {
                //PlayMakerFSM playMakerFSM = Mod_PortableLibrary.uiDocumentsGO.GetComponent<PlayMakerFSM>();
                //Fsm fsm = playMakerFSM.Fsm;
                //fsm.Variables.GetFsmGameObject("Clicked Document").Value = menuNode.dummyDocumentGO;
                //playMakerFSM.SendEvent("Ui Doc");
                //if (Mod_PortableLibrary.allwaysMagnify.Value)
                //{
                //    playMakerFSM.SendEvent("Turn on Mag");
                //}
                //openedBook = menuNode.dummyDocumentGO;

                openedBook = menuNode.uiDocumentGO;
                openedBook.active = true;
                Mod_DocumentCollector.EnableArrows();
                var t = inventoryGO.GetComponent<PlayMakerArrayListProxy>();
                bool haveMag = false;//meh
                foreach (var i in t.arrayList)
                {
                    var j = i.Cast<GameObject>();
                    if (j.name.Equals("MAGNIFYING GLASS"))
                    {
                        haveMag = true;
                        break;
                    }
                }
                if (haveMag || allwaysMagnify.Value)
                {
                    magnifyGlassGO.active = true;
                }
                menuOn = false;
            }
        }

        private void CloseBook()
        {
            if (openedBook != null)
            {
                //Mod_PortableLibrary.uiDocumentsGO.GetComponent<PlayMakerFSM>().SendEvent("disable");//not great? sends quickunfreeze
                //GameObject.Find(Mod_PortableLibrary.fpsCharPath)?.GetComponent<PlayMakerFSM>()?.SendEvent("QuickFreeze");

                openedBook.active = false;
                Mod_DocumentCollector.DisableArrows();
                //Document Exit.active = false;
                magnifyGlassGO.active = false;
                magnifyGlassGO.GetComponent<PlayMakerFSM>()?.SendEvent("enable");//not sure why they do it, maybe to reset it to position?
                openedBook = null;
                menuOn = true;
            }
        }

        private void OnNodeActivated(MenuNode menuNode)
        {
            if (menuNode.IsLeaf)
            {
                OpenBook(menuNode);
            }
            else
            {
                menuNode.Expanded = !menuNode.Expanded;
            }
        }

        private void OnNodeDeactivated(MenuNode menuNode)
        {
            if (menuNode.Expanded)
            {
                menuNode.Expanded = !menuNode.Expanded;
            }
            else
            {
                int parentIndex = visibleNodes.IndexOf(menuNode.Parent);
                if (parentIndex >= 0)
                {
                    selectedIndex = parentIndex;
                }
            }
        }
    }

    [Serializable]
    public class KeyBindings
    {
        public KeyCode activateMenu = KeyCode.L;
        public KeyCode up = KeyCode.UpArrow;
        public KeyCode down = KeyCode.DownArrow;
        public KeyCode right = KeyCode.RightArrow;
        public KeyCode left = KeyCode.LeftArrow;
        public KeyCode select = KeyCode.Return;
        public KeyCode exit = KeyCode.Escape;
    }

    public static class MiniJsonUtil
    {
        public static string ToJson<T>(T obj)
        {
            if (obj == null)
                return "null";

            var type = typeof(T);
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);

            var sb = new StringBuilder(128);
            sb.Append('{');

            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                object value = field.GetValue(obj);

                sb.Append('"');
                sb.Append(field.Name);
                sb.Append("\":");

                WriteValue(sb, value);

                if (i < fields.Length - 1)
                    sb.Append(',');
            }

            sb.Append('}');
            return sb.ToString();
        }

        private static void WriteValue(StringBuilder sb, object value)
        {
            if (value == null)
            {
                sb.Append("null");
                return;
            }

            Type t = value.GetType();

            if (t == typeof(string))
            {
                sb.Append('"');
                sb.Append(Escape((string)value));
                sb.Append('"');
            }
            else if (t.IsEnum)
            {
                sb.Append('"');
                sb.Append(value.ToString());
                sb.Append('"');
            }
            else if (t == typeof(bool))
            {
                sb.Append((bool)value ? "true" : "false");
            }
            else if (t.IsPrimitive)
            {
                sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
            }
            else
            {
                throw new NotSupportedException($"Type {t} is not supported");
            }
        }

        private static string Escape(string s)
        {
            return s
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        public static T FromJson<T>(string json) where T : new()
        {
            var obj = new T();
            var type = typeof(T);

            json = json.Trim().TrimStart('{').TrimEnd('}');
            var pairs = json.Split(',');

            foreach (var pair in pairs)
            {
                var kv = pair.Split(new[] { ':' }, 2);
                if (kv.Length != 2)
                    continue;

                string key = TrimQuotes(kv[0]);
                string value = kv[1].Trim();

                FieldInfo field = type.GetField(key, BindingFlags.Public | BindingFlags.Instance);
                if (field == null)
                    continue;

                object parsed = ParseValue(field.FieldType, value);
                field.SetValue(obj, parsed);
            }

            return obj;
        }

        private static object ParseValue(Type type, string value)
        {
            if (value == "null")
                return null;

            if (type == typeof(string))
                return Unescape(TrimQuotes(value));

            if (type == typeof(bool))
                return value == "true";

            if (type.IsEnum)
                return Enum.Parse(type, TrimQuotes(value));

            if (type.IsPrimitive)
                return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);

            throw new NotSupportedException($"Type {type} is not supported");
        }

        private static string TrimQuotes(string s)
        {
            s = s.Trim();
            if (s.StartsWith("\"")) s = s.Substring(1);
            if (s.EndsWith("\"")) s = s.Substring(0, s.Length - 1);
            return s;
        }

        private static string Unescape(string s)
        {
            return s
                .Replace("\\n", "\n")
                .Replace("\\r", "\r")
                .Replace("\\t", "\t")
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");
        }
    }
}