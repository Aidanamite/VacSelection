using HarmonyLib;
using SRML;
using SRML.SR;
using SRML.Console;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using InControl;
using TMPro;
using SRML.Config.Attributes;

namespace VacSelection
{
    public class Main : ModEntryPoint
    {
        internal static Assembly modAssembly = Assembly.GetExecutingAssembly();
        internal static string modName = $"{modAssembly.GetName().Name}";
        internal static string modDir = $"{System.Environment.CurrentDirectory}\\SRML\\Mods\\{modName}";
        internal static VacSelection select;
        public const string fileName = "interal config";
        internal static Sprite selectorUI;
        internal static Sprite selectorBackUI;
        internal static List<VacSprite> groupSprites = new List<VacSprite>();
        internal static vacSelectionListParser parser;
        internal static Dictionary<List<VacSelection>, string> preloadCache = new Dictionary<List<VacSelection>, string>();
        internal static Console.ConsoleInstance ConsoleI;
        internal static PlayerAction up;
        internal static PlayerAction down;

        static int mod(int a, int b) { a %= b; return a < 0 ? a + b : a; }

        public Main()
        {
            parser = new vacSelectionListParser();
            SRML.Config.Parsing.ParserRegistry.RegisterParser(parser);
        }

        public override void PreLoad()
        {
            ConsoleI = ConsoleInstance;
            groupSprites.Add(new VacSprite("any", LoadImage("any_icon.png", 1024, 1024).CreateSprite()));
            groupSprites.Add(new VacSprite("food", LoadImage("any_food_icon.png", 525, 525).CreateSprite()));
            var s = Resources.FindObjectsOfTypeAll<Sprite>().Find(
                (x) => x.name == "iconCategoryVeggie",
                (x) => x.name == "iconCategoryPlort",
                (x) => x.name == "iconCategoryFruit",
                (x) => x.name == "iconCategoryMeat",
                (x) => x.name == "iconEchoesPedia",
                (x) => x.name == "iconDecorizerOrnaments",
                (x) => x.name == "iconEchoNotesPedia");
            groupSprites.Add(new VacSprite("veggie", s[0]));
            groupSprites.Add(new VacSprite("plort", s[1]));
            groupSprites.Add(new VacSprite("fruit", s[2]));
            groupSprites.Add(new VacSprite("meat", s[3]));
            groupSprites.Add(new VacSprite("echo", s[4]));
            groupSprites.Add(new VacSprite("ornament", s[5]));
            groupSprites.Add(new VacSprite("note", s[6]));
            selectorUI = LoadImage("selector.png", 2048, 4192).CreateSprite();
            selectorBackUI = LoadImage("selector_back.png", 2048, 4192).CreateSprite();
            HarmonyInstance.PatchAll();
            Console.RegisterCommand(new CustomCommand());
            Console.RegisterCommand(new CustomCommand2());
            Console.RegisterCommand(new CustomCommand3());
            (up = BindingRegistry.RegisterBindedAction("key.vacFilterUp")).AddDefaultBinding(Key.Z);
            TranslationPatcher.AddUITranslation("key.key.vacfilterup", "Cycle Vac Selector Up");
            (down = BindingRegistry.RegisterBindedAction("key.vacFilterDown")).AddDefaultBinding(Key.X);
            TranslationPatcher.AddUITranslation("key.key.vacfilterdown", "Cycle Vac Selector Down");
        }
        public override void Load()
        {
            if (preloadCache.Count == 0)
            {
                preloadCache = null;
                Config2.Reset();
            }
            else
            {
                var c = preloadCache;
                preloadCache = null;
                foreach (var p in c)
                    p.Key.AddRange((List<VacSelection>)parser.ParseObject(p.Value));
            }
            select = Config2.options.First();
        }
        public override void Update()
        {
            if (up.WasPressed)
                select = Config2.options[mod(Config2.options.IndexOf(select) + 1, Config2.options.Count)];
            if (down.WasPressed)
                select = Config2.options[mod(Config2.options.IndexOf(select) - 1, Config2.options.Count)];
        }
        public static void Log(string message) => ConsoleI.Log($"[{modName}]: " + message);
        public static void LogError(string message) => ConsoleI.LogError($"[{modName}]: " + message);
        public static void LogWarning(string message) => ConsoleI.LogWarning($"[{modName}]: " + message);
        public static void LogSuccess(string message) => ConsoleI.LogSuccess($"[{modName}]: " + message);

        public static Sprite GetIcon(Identifiable.Id id) => SRSingleton<GameContext>.Instance.LookupDirector.GetIcon(id);
        public static Sprite GetIcon(Gadget.Id id) => SRSingleton<GameContext>.Instance.LookupDirector.GetGadgetDefinition(id).icon;
        public static Sprite GetIcon(ExchangeDirector.NonIdentReward id) => SRSingleton<SceneContext>.Instance.ExchangeDirector.GetSpecRewardIcon(id);
        public static Texture2D LoadImage(string filename, int width, int height)
        {
            var spriteData = modAssembly.GetManifestResourceStream(modName + "." + filename);
            var rawData = new byte[spriteData.Length];
            spriteData.Read(rawData, 0, rawData.Length);
            var tex = new Texture2D(width, height);
            tex.LoadImage(rawData);
            return tex;
        }
    }

    class VacSprite
    {
        public Sprite sprite;
        public string name;
        public VacSprite(string Name, Sprite Sprite)
        {
            name = Name;
            sprite = Sprite;
        }
    }

    public class VacSelection
    {
        public Identifiable.Id[] ids = new Identifiable.Id[0];
        public VacGroup groups = VacGroup.None;
        public string name = "";
        public Sprite sprite = null;
        string _s = "";
        public string spriteName
        {
            get => _s;
            set
            {
                var SpriteName = value;
                Sprite s = null;
                if (SpriteName.StartsWith("g:"))
                {
                    SpriteName = SpriteName.Remove(0, 2);
                    s = Main.groupSprites.FirstOrDefault((x) => x.name.ToLowerInvariant() == SpriteName.ToLowerInvariant()).sprite;
                    if (!s)
                        Main.LogError($"Could not find group icon \"{SpriteName}\"");
                }
                else if (SpriteName.StartsWith("i:"))
                {
                    SpriteName = SpriteName.Remove(0, 2);
                    if (System.Enum.TryParse(SpriteName, true, out Identifiable.Id item))
                    {
                        if (Identifiable.IsSlime(item))
                            s = SceneContext.Instance.SlimeAppearanceDirector.GetChosenSlimeAppearance(item).Icon;
                        else if (GameContext.Instance.LookupDirector.vacItemDict.TryGetValue(item, out var def))
                            s = def.Icon;
                        if (!s)
                            Main.LogError($"Item \"{SpriteName}\" does not have an icon");
                    }
                    else
                        Main.LogError($"Could not find item \"{SpriteName}\"");
                }
                else if (SpriteName.StartsWith("p:"))
                {
                    SpriteName = SpriteName.Remove(0, 2);
                    if (System.Enum.TryParse(SpriteName, true, out Gadget.Id item))
                    {
                        if (GameContext.Instance.LookupDirector.gadgetDefinitionDict.TryGetValue(item, out var def))
                            s = def.icon;
                        if (!s)
                            Main.LogError($"Gadget \"{SpriteName}\" does not have an icon");
                    }
                    else
                        Main.LogError($"Could not find gadget \"{SpriteName}\"");
                }
                else if (SpriteName.StartsWith("e:"))
                {
                    SpriteName = SpriteName.Remove(0, 2);
                    if (System.Enum.TryParse(SpriteName, true, out ExchangeDirector.NonIdentReward item))
                    {
                        if (SceneContext.Instance.ExchangeDirector.nonIdentRewardDict.TryGetValue(item, out var def))
                            s = def;
                        if (!s)
                            Main.LogError($"Reward \"{SpriteName}\" does not have an icon");
                    }
                    else
                        Main.LogError($"Could not find Non-Identifiable Reward \"{SpriteName}\"");
                }
                else if (SpriteName.StartsWith("s:"))
                {
                    SpriteName = SpriteName.Remove(0, 2);
                    s = Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault((x) => x.name == SpriteName);
                    if (!s)
                        Main.LogError($"Could not find sprite \"{SpriteName}\"");
                }
                if (s)
                {
                    _s = value;
                    sprite = s;
                }
            }
        }
        public bool Includes(Identifiable.Id id) => ids.Contains(id) || groups.Includes(id) || (ids.Length == 0 && groups == VacGroup.None);
        public VacSelection() { }
        public VacSelection(string[] value)
        {
            name = value[0];
            spriteName = value[1];
            value[0] = "";
            value[1] = "";
            var Ids = new List<Identifiable.Id>();
            foreach (var s in value)
            {
                if (s.StartsWith("i:"))
                {
                    if (System.Enum.TryParse(s.Remove(0, 2), true, out Identifiable.Id id))
                        Ids.Add(id);
                    else
                        Main.ConsoleI.LogWarning(s.Remove(0, 2) + " is not a valid item");
                }
                else if (s.StartsWith("g:"))
                {
                    if (System.Enum.TryParse(s.Remove(0, 2), true, out VacGroup group))
                        groups |= group;
                    else
                        Main.ConsoleI.LogWarning(s.Remove(0, 2) + " is not a valid group name");
                }
            }
            ids = Ids.ToArray();
        }
        public static List<string> GetSpriteNames()
        {
            var l = new List<string>();
            l.AddRangeUnique(Main.groupSprites.GetAll((x) => new string[] { "g:" + x.name }));
            l.AddRangeUnique(System.Enum.GetNames(typeof(Identifiable.Id)).GetAll((x) => new string[] { "i:" + x }));
            l.AddRangeUnique(System.Enum.GetValues(typeof(Gadget.Id)).Cast<Gadget.Id>().GetAll((x) => new string[] { "p:" + x }));
            l.AddRangeUnique(System.Enum.GetValues(typeof(ExchangeDirector.NonIdentReward)).Cast<ExchangeDirector.NonIdentReward>().GetAll((x) => new string[] { "e:" + x }));
            return l;
        }
        public static List<string> GetFilterNames()
        {
            var l = new List<string>();
            l.AddRangeUnique(System.Enum.GetNames(typeof(VacGroup)).GetAll((x) => new string[] { "g:" + x }));
            l.AddRangeUnique(System.Enum.GetNames(typeof(Identifiable.Id)).GetAll((x) => new string[] { "i:" + x }));
            return l;
        }
    }

    static class ExtentionMethods
    {
        public static Sprite CreateSprite(this Texture2D texture) => Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 1);
        public static List<T> ToList<T>(this T[] array) => new List<T>(array);

        public static bool CanVac(this Identifiable.Id id) => Main.select.Includes(id);

        public static bool Includes(this VacGroup a, VacGroup b) => (a & b) > 0;
        public static bool Includes(this VacGroup a, Identifiable.Id id) =>
            (Main.select.groups.Includes(VacGroup.Plort) && Identifiable.IsPlort(id))
            || (Main.select.groups.Includes(VacGroup.Slime) && Identifiable.IsSlime(id))
            || (Main.select.groups.Includes(VacGroup.Resource) && Identifiable.IsCraft(id))
            || (Main.select.groups.Includes(VacGroup.Meat) && Identifiable.MEAT_CLASS.Contains(id))
            || (Main.select.groups.Includes(VacGroup.Veggie) && Identifiable.IsVeggie(id))
            || (Main.select.groups.Includes(VacGroup.Fruit) && Identifiable.IsFruit(id))
            || (Main.select.groups.Includes(VacGroup.Crate) && id.ToString().Contains("CRATE"))
            || (Main.select.groups.Includes(VacGroup.Chick) && Identifiable.IsChick(id))
            || (Main.select.groups.Includes(VacGroup.Food) && Identifiable.IsFood(id))
            || (Main.select.groups.Includes(VacGroup.Echo) && Identifiable.IsEcho(id))
            || (Main.select.groups.Includes(VacGroup.Ornament) && Identifiable.IsOrnament(id))
            || (Main.select.groups.Includes(VacGroup.Note) && Identifiable.IsEchoNote(id));

        public static T GetOrAddComponent<T>(this GameObject obj) where T : Component => obj.GetComponent<T>() == null ? obj.AddComponent<T>() : obj.GetComponent<T>();

        public static List<FieldInfo> GetAllFeilds(this object value)
        {
            var l = new List<FieldInfo>();
            var t = value.GetType();
            while (t != typeof(object))
            {
                foreach (var f in t.GetFields((BindingFlags)(-1)))
                    if (!f.IsStatic)
                        l.Add(f);
                t = t.BaseType;
            }
            return l;
        }

        public static T[] Find<T>(this IEnumerable<T> c, params System.Predicate<T>[] preds)
        {
            var f = new T[preds.Length];
            if (preds.Length == 0)
                return f;
            foreach (var i in c)
            {
                var k = 0;
                for (int j = 0; j < preds.Length; j++)
                {
                    if (f[j] != null)
                    {
                        k++;
                        continue;
                    }
                    if (preds[j](i))
                    {
                        k++;
                        f[j] = i;
                    }
                }
                if (k == preds.Length)
                    continue;
            }
            return f;
        }
        public static List<Y> GetAll<X, Y>(this IEnumerable<X> os, System.Func<X, IEnumerable<Y>> collector, bool ignoreDuplicates = true)
        {
            var l = new List<Y>();
            foreach (var o in os)
            {
                var c = collector(o);
                if (c != null)
                {
                    if (ignoreDuplicates)
                        l.AddRangeUnique(c);
                    else
                        l.AddRange(c);
                }
            }
            return l;
        }

        public static void AddRangeUnique<X>(this List<X> c, IEnumerable<X> collection)
        {
            foreach (var i in collection)
                if (!c.Contains(i))
                    c.Add(i);
        }
    }

    public enum VacGroup
    {
        None,
        Plort = 1,
        Slime = 2,
        Resource = 4,
        Meat = 8,
        Veggie = 16,
        Fruit = 32,
        Crate = 64,
        Chick = 128,
        Food = 256,
        Echo = 512,
        Ornament = 1024,
        Note = 2048,
        Animal = Meat | Chick,
        Ingredients = Plort | Resource
    }

    class CustomCommand : ConsoleCommand
    {
        public override string Usage => "selector <name>";
        public override string ID => "selector";
        public override string Description => "Sets the selected mode for the vac filter";
        public override bool Execute(string[] args)
        {
            if (args == null || args.Length < 1)
            {
                Main.LogError("Not enough arguments");
                return false;
            }
            var ind = Config2.options.FindIndex((x) => x.name == args[0]);
            if (ind >= 0) {
                    Main.select = Config2.options[ind];
                    Main.LogSuccess("Selector is now " + args[0]);
            }
            else
                Main.LogError($"{args[0]} is not a valid selector");
            return true;
        }
        public override List<string> GetAutoComplete(int argIndex, string argText)
        {
            if (argIndex == 0)
                return System.Enum.GetNames(typeof(VacGroup)).ToList();
            return base.GetAutoComplete(argIndex, argText);
        }
    }

    class CustomCommand2 : ConsoleCommand
    {
        public override string Usage => "setselector <name> <icon> <filters...>";
        public override string ID => "setselector";
        public override string Description => "Sets the information for a vac filters option";
        public override bool Execute(string[] args)
        {
            if (args == null || args.Length < 3)
            {
                Main.LogError("Not enough arguments");
                return false;
            }
            VacSelection ns;
            try
            {
                ns = new VacSelection(args);
            } catch
            {
                Main.LogError("Failed to parse args to selector");
                return true;
            }
            var i = Config2.options.FindIndex((x) => x.name == ns.name);
            if (i == -1)
            {
                Config2.options.Add(ns);
                foreach (var c in SRModLoader.GetModForAssembly(Main.modAssembly).Configs) c.SaveToFile();
                if (SelectorUI.instance)
                    SelectorUI.instance.GenerateOptions();
                Main.LogSuccess($"Option \"{ns.name}\" added");
            }
            else
            {
                Config2.options[i] = ns;
                foreach (var c in SRModLoader.GetModForAssembly(Main.modAssembly).Configs) c.SaveToFile();
                if (SelectorUI.instance)
                    SelectorUI.instance.UpdateOption(i);
                Main.LogSuccess($"Option \"{ns.name}\" changed");
            }
            return true;
        }
        public override List<string> GetAutoComplete(int argIndex, string argText)
        {
            if (argIndex == 0)
                return Config2.options.GetAll((x) => new string[] { x.name });
            if (argIndex == 1)
                return VacSelection.GetSpriteNames();
            return VacSelection.GetFilterNames();
        }
    }

    class CustomCommand3 : ConsoleCommand
    {
        public override string Usage => "removeselector <name>";
        public override string ID => "removeselector";
        public override string Description => "Removes an option from the vac filter options";
        public override bool Execute(string[] args)
        {
            if (args == null || args.Length < 1)
            {
                Main.LogError("Not enough arguments");
                return false;
            }
            var i = Config2.options.FindIndex((x) => x.name == args[0]);
            if (i == -1)
                Main.LogError($"No option found for \"{args[0]}\"");
            else
            {
                if (Main.select == Config2.options[i])
                    Main.select = Config2.options.First();
                Config2.options.RemoveAt(i);
                foreach (var c in SRModLoader.GetModForAssembly(Main.modAssembly).Configs) c.SaveToFile();
                if (SelectorUI.instance)
                    SelectorUI.instance.GenerateOptions();
                Main.LogSuccess($"Option \"{args[0]}\" removed");
            }
            return true;
        }
        public override List<string> GetAutoComplete(int argIndex, string argText)
        {
            if (argIndex == 0)
                return Config2.options.GetAll((x) => new string[] { x.name });
            return base.GetAutoComplete(argIndex, argText);
        }
    }

    class SelectorUI : MonoBehaviour
    {
        public static SelectorUI instance;
        RectTransform spinPivot;
        Quaternion start;
        Quaternion target;
        float current;
        int lastInd = -1;
        RectTransform rect;
        RectTransform hudRect;
        TMP_Text text;
        RectTransform textRect;
        void Awake() => instance = this;
        void Start()
        {
            hudRect = SRSingleton<HudUI>.Instance.GetComponent<RectTransform>();
            rect = gameObject.GetOrAddComponent<RectTransform>();
            var image = gameObject.AddComponent<Image>();
            image.sprite = Main.selectorBackUI;
            image.useSpriteMesh = true;
            rect.anchorMin = new Vector2(1, 0.3f);
            rect.anchorMax = rect.anchorMin;
            spinPivot = new GameObject("pivot").GetOrAddComponent<RectTransform>();
            spinPivot.SetParent(rect, false);
            spinPivot.sizeDelta = Vector2.zero;
            spinPivot.anchorMin = new Vector2(1 - 1 / 4f, 0.5f - 1 / 8f);
            spinPivot.anchorMax = spinPivot.anchorMin + new Vector2(1 / 2f, 1 / 4f);

            GenerateOptions();

            var over = new GameObject("overlay", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            over.SetParent(rect, false);
            over.sizeDelta = Vector2.zero;
            over.anchorMax = Vector2.one;
            over.anchorMin = Vector2.zero;
            over.GetComponent<Image>().sprite = Main.selectorUI;
            text = Instantiate(SRSingleton<HudUI>.Instance.currencyText,rect,false).GetComponent<TMP_Text>();
            textRect = text.GetComponent<RectTransform>();
            textRect.sizeDelta = Vector2.zero;
            textRect.anchorMin = Vector2.up * 0.5f;
            textRect.anchorMax = Vector2.up * 0.5f;
            text.lineSpacing = 0;
            text.autoSizeTextContainer = false;
        }
        void Update()
        {
            var s = hudRect.rect.height * Config.guiSize;
            rect.offsetMin = new Vector2(-s / 2, -s / 2);
            rect.offsetMax = new Vector2(0, s / 2);
            int i = Config2.options.FindIndex((x) => x == Main.select);
            if (lastInd != i)
            {
                lastInd = i;
                SetTarget(i);
            }
            textRect.offsetMin = new Vector2(-text.preferredWidth - text.preferredHeight / 2, -text.preferredHeight / 2);
            textRect.offsetMax = new Vector2(0, text.preferredHeight / 2);
            if (current < 1)
                current += Time.deltaTime * 3;
            if (current > 1)
                current = 1;
            spinPivot.localRotation = Quaternion.Lerp(start, target, current);
            var d = 360f / Config2.options.Count;
            foreach (RectTransform t in spinPivot)
            {
                t.localScale = Vector3.one * (1 - Mathf.Min(AngleDifference(spinPivot.localRotation, t.localRotation) / d, 1) * 0.5f);
                t.anchorMin = new Vector2(-11 / 8f / t.localScale.x, 0);
                t.anchorMax = t.anchorMin + Vector2.one;
                t.pivot = new Vector2(11 / 8f / t.localScale.x + 0.5f, 0.5f);
            }
        }
        static float AngleDifference(Quaternion a, Quaternion b)
        {
            var r = (a * b).eulerAngles.z;
            return Mathf.Abs(r > 180 ? r - 360 : r);
        }
        void SetTarget(int i)
        {
            text.text = Main.select.name;
            //foreach (Transform t in spinPivot)
                //t.localScale = Vector3.one * (i == t.GetSiblingIndex() ? 1 : 0.7f);
            start = spinPivot.localRotation;
            var d = 360f / Config2.options.Count;
            target = Quaternion.Euler(0, 0, d * -i);
            current = 0;
        }
        public void GenerateOptions()
        {
            foreach (Transform t in spinPivot)
                Destroy(t.gameObject);
            int i = 0;
            var d = 360f / Config2.options.Count;
            foreach (var t in Config2.options)
            {
                var gO = new GameObject("icon", typeof(RectTransform), typeof(Image));
                gO.GetComponent<Image>().sprite = t.sprite;
                var r = gO.GetComponent<RectTransform>();
                r.SetParent(spinPivot, false);
                r.sizeDelta = Vector2.zero;
                r.anchorMin = new Vector2(-11 / 8f, 0);
                r.anchorMax = r.anchorMin + Vector2.one;
                r.pivot = new Vector2(15 / 8f, 0.5f);
                r.localRotation = Quaternion.Euler(0, 0, d * i);
                i++;
            }
        }
        public void UpdateOption(int index)
        {
            var i = index;
            foreach (Transform t in spinPivot)
                if (i-- == 0)
                    t.GetComponent<Image>().sprite = Config2.options[index].sprite;
        }
    }

    [HarmonyPatch(typeof(WeaponVacuum),"Update")]
    class Patch_VacUpdate
    {
        public static bool calling = false;
        static void Prefix() => calling = true;
        static void Postfix() => calling = false;
    }

    [HarmonyPatch(typeof(TrackCollisions), "CurrColliders")]
    class Patch_GetCollided
    {
        static void Postfix(TrackCollisions __instance, ref HashSet<GameObject> __result)
        {
            if ((Main.select.groups == VacGroup.None && Main.select.ids.Length == 0) || !__instance.GetComponentInParent<TeleportablePlayer>())
                return;
            var n = new HashSet<GameObject>();
            foreach (var i in __result)
                if (!i.GetComponent<Vacuumable>() || i.GetComponent<Vacuumable>().identifiable.id.CanVac())
                    n.Add(i);
            __result = n;
        }
    }

    [HarmonyPatch(typeof(HudUI),"Start")]
    class Patch_HudStart
    {
        static void Postfix(HudUI __instance) => new GameObject("SelectorUI", typeof(SelectorUI)).transform.SetParent(__instance.transform, false);
    }

    [ConfigFile("general")]
    public static class Config {
        public static float guiSize = 0.3f;
    }

    [HarmonyPatch(typeof(SiloCatcher), "OnTriggerStay")]
    class Patch_SiloPull
    {
        public static bool calling = false;
        static void Prefix() => calling = true;
        static void Postfix() => calling = false;
    }

    [HarmonyPatch(typeof(Ammo), "GetSelectedId")]
    class Patch_GetAmmoId
    {
        static void Postfix(ref Identifiable.Id __result)
        {
            if (Main.select.groups == VacGroup.None && Main.select.ids.Length == 0)
                return;
            if (Patch_SiloPull.calling && !__result.CanVac())
                __result = Identifiable.Id.NONE;
        }
    }

    [HarmonyPatch(typeof(DecorizerStorage), "selected",MethodType.Getter)]
    class Patch_GetDecorizerId
    {
        static void Postfix(ref Identifiable.Id __result)
        {
            if (Main.select.groups == VacGroup.None && Main.select.ids.Length == 0)
                return;
            if (Patch_SiloPull.calling && !__result.CanVac())
                __result = Identifiable.Id.NONE;
        }
    }

    [HarmonyPatch(typeof(GlitchStorage), "Remove")]
    class Patch_GetGlitchStorageId
    {
        static bool Prefix(GlitchStorage __instance, ref bool __result, ref Identifiable.Id id)
        {
            if (Main.select.groups == VacGroup.None && Main.select.ids.Length == 0)
                return true;
            if (Patch_SiloPull.calling && !__instance.model.id.CanVac())
            {
                __result = false;
                id = Identifiable.Id.NONE;
                return false;
            }
            return true;
        }
    }

    [ConfigFile(Main.fileName)]
    public static class Config2
    {
        public static List<VacSelection> options = new List<VacSelection>();
        public static void Reset()
        {
            options = new List<VacSelection>();
            options.Add(new VacSelection() { name = "None", groups = VacGroup.None, spriteName = "g:any" });
            options.Add(new VacSelection() { name = "Plort", groups = VacGroup.Plort, spriteName = "g:plort" });
            options.Add(new VacSelection() { name = "Slime", groups = VacGroup.Slime, spriteName = "i:PINK_SLIME" });
            options.Add(new VacSelection() { name = "Resource", groups = VacGroup.Resource, spriteName = "i:JELLYSTONE_CRAFT" });
            options.Add(new VacSelection() { name = "Meat", groups = VacGroup.Meat, spriteName = "g:meat" });
            options.Add(new VacSelection() { name = "Veggie", groups = VacGroup.Veggie, spriteName = "g:veggie" });
            options.Add(new VacSelection() { name = "Fruit", groups = VacGroup.Fruit, spriteName = "g:fruit" });
            options.Add(new VacSelection() { name = "Crate", groups = VacGroup.Crate, spriteName = "e:NEWBUCKS_HUGE" });
            options.Add(new VacSelection() { name = "Food", groups = VacGroup.Food, spriteName = "g:food" });
            options.Add(new VacSelection() { name = "Ingredients", groups = VacGroup.Ingredients, spriteName = "p:EXTRACTOR_DRILL_NOVICE" });
        }
    }

    public class vacSelectionListParser : SRML.Config.Parsing.IStringParser
    {
        public object ParseObject(string value)
        {
            var d = new List<VacSelection>();
            if (value == "")
                return d;
            if (Main.preloadCache != null)
            {
                Main.preloadCache[d] = value;
                return d;
            }
            var pairs = value.Split(';');
            foreach (var pair in pairs)
            {
                if (pair == "")
                    continue;
                var data = pair.Split('|');
                try
                {
                    var ids = data[3].Split(',');
                    var group = (VacGroup)int.Parse(data[2]);
                    List<Identifiable.Id> ids2 = new List<Identifiable.Id>();
                    foreach (var i in ids)
                        if (System.Enum.TryParse(i, out Identifiable.Id i2) && i2 != Identifiable.Id.NONE)
                            ids2.Add(i2);
                        else
                        {
                            string i3 = "";
                            if (EnumTranslator.OnTranslationFallback(typeof(Identifiable.Id), ref i3) && System.Enum.TryParse(i3, out i2) && i2 != Identifiable.Id.NONE)
                                ids2.Add(i2);
                        }
                    d.Add(new VacSelection() { groups = group, ids = ids2.ToArray(), name = data[0], spriteName = data[1] });
                }
                catch { }
            }
            return d;
        }
        public string EncodeObject(object value)
        {
            if (!type.IsInstanceOfType(value))
                return "";
            var data = "";
            foreach (var pair in value as List<VacSelection>)
                data += (data == "" ? "" : ";") + pair.name + "|" + pair.spriteName + "|" + (int)pair.groups + "|" + pair.ids.Join(null,",");
            return data;
        }
        public string GetUsageString() => type.Name;
        public System.Type ParsedType => type;
        static System.Type type = typeof(List<VacSelection>);
    }
}