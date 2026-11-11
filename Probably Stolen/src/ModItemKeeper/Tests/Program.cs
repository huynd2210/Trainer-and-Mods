using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModItemKeeper;

// Usage: Tests <save.es3> [<save.es3> ...]
// Reads saves only. For every inventory list in each save, pretends a set of item ids belongs
// to a removed mod, cuts their contents out, round-trips the cut through JSON (as the mod's
// stash does), renumbers the remaining list as a re-save would, restores, and checks the item
// tree is exactly what it was.
internal static class Program
{
    private static int failures;
    private static int checks;

    private static int Main(string[] args)
    {
        Synthetic();
        foreach (string path in args)
            RealSave(path);
        Console.WriteLine($"{checks} checks, {failures} failures");
        return failures == 0 ? 0 : 1;
    }

    // ---- Real saves -------------------------------------------------------------------

    private static void RealSave(string path)
    {
        // ES3 files aren't strict JSON (unquoted numeric keys); take the inventory strings out directly.
        string text = File.ReadAllText(path);
        var fields = System.Text.RegularExpressions.Regex.Matches(text, "\"(\\w+InvJSON)\":\"((?:[^\"\\\\]|\\\\.)*)\"");
        Check(fields.Count >= 4, Path.GetFileName(path), "found the inventory lists");
        foreach (System.Text.RegularExpressions.Match field in fields)
        {
            string json = JsonNode.Parse("\"" + field.Groups[2].Value + "\"").GetValue<string>();
            if (string.IsNullOrEmpty(json))
                continue;
            string label = Path.GetFileName(path) + ":" + field.Groups[1].Value;

            // The removed mod: Agri Expansion's prefixed items. And a harsher case: a big
            // vanilla container (with nested containers) treated as unknown.
            RoundTrip(label + " [agri_*]", json, id => !id.StartsWith("agri_"));
            RoundTrip(label + " [backpack_large+agri]", json, id => id != "backpack_large" && !id.StartsWith("agri_"));
        }
    }

    private static void RoundTrip(string label, string json, Func<string, bool> knownWhileRemoved)
    {
        List<JsonObject> nodes = Load(json);
        string before = Canonical(nodes);
        int countBefore = nodes.Count;

        List<Cut<JsonObject>> cuts = KeeperCore.CutUnknownContents(nodes, A, n => knownWhileRemoved(A.Id(n)), Clone);
        if (cuts.Count == 0)
            return;
        int inside = cuts.Sum(c => c.Nodes.Count - 1);
        Check(nodes.Count + inside == countBefore, label, "node count conserved");
        Check(NoDanglingRefs(nodes), label, "no dangling child refs after cut");
        Check(cuts.All(c => nodes.Any(n => A.UniqueId(n) == c.ParentUniqueId && A.Children(n).Count == 0)),
            label, "containers stay, emptied");

        // While the mod is missing the game would load and re-save this list; do the same.
        List<Cut<JsonObject>> stashed = cuts.Select(c => new Cut<JsonObject>
        {
            ParentUniqueId = c.ParentUniqueId,
            ParentId = c.ParentId,
            Nodes = Load(Save(c.Nodes)),
        }).ToList();
        nodes = Load(Save(ResaveRenumber(nodes)));

        // Trees for "container gone": each node of the cut exactly once.
        foreach (Cut<JsonObject> c in stashed)
        {
            var trees = KeeperCore.DirectChildTrees(c, A);
            var flat = trees.SelectMany(t => t).Select(A.Uuid).ToList();
            Check(flat.Count == c.Nodes.Count - 1 && flat.Distinct().Count() == flat.Count, label, "loose trees cover the cut once");
        }

        // Mod back: restore everything (nested containers are inside the outer cut).
        foreach (Cut<JsonObject> c in stashed)
            Check(KeeperCore.Restore(nodes, c, A, _ => true), label, $"restore {c.ParentId}#{c.ParentUniqueId}");
        Check(NoDanglingRefs(nodes), label, "no dangling child refs after restore");
        Check(nodes.Select(A.Uuid).Distinct().Count() == nodes.Count, label, "uuids unique after restore");
        string after = Canonical(nodes);
        Check(after == before, label, "tree identical after round trip");
        Console.WriteLine($"  {label}: cut {cuts.Count} container(s), {inside} item(s) inside -> restored");
    }

    // The game numbers uuids afresh on every save; simulate that with a shuffled renumbering.
    private static List<JsonObject> ResaveRenumber(List<JsonObject> nodes)
    {
        Random rng = new Random(7);
        List<JsonObject> order = new List<JsonObject> { nodes[0] };
        order.AddRange(nodes.Skip(1).OrderBy(_ => rng.Next()));
        Dictionary<long, long> map = new Dictionary<long, long>();
        long next = 1000;
        foreach (JsonObject n in order)
            map[A.Uuid(n)] = next += 3;
        foreach (JsonObject n in order)
        {
            A.SetUuid(n, map[A.Uuid(n)]);
            A.SetChildren(n, A.Children(n).Select(c => map[c]).ToList());
        }
        return order;
    }

    // ---- Synthetic edge cases ---------------------------------------------------------

    private static void Synthetic()
    {
        // root(0) -> modbox(1){ a(2), modbox2(3){ b(4) } }, plain(5){ c(6) }, modempty(7)
        List<JsonObject> nodes = new List<JsonObject>
        {
            N("save_bag", 0, 0, (1, 1), (5, 1), (7, 1)),
            N("mod_box", 1, 101, (2, 4), (3, 6)),
            N("apple", 2, 102),
            N("mod_box2", 3, 103, (4, 2)),
            N("bean", 4, 104),
            N("crate", 5, 105, (6, 1)),
            N("coin", 6, 106),
            N("mod_empty", 7, 107),
        };
        string before = Canonical(nodes);
        var cuts = KeeperCore.CutUnknownContents(nodes, A, n => !A.Id(n).StartsWith("mod_"), Clone);
        Check(cuts.Count == 1 && cuts[0].ParentUniqueId == 101, "synthetic", "only the outer unknown container is cut");
        Check(cuts[0].Nodes.Count == 4, "synthetic", "cut holds container + 3 inside");
        Check(nodes.Count == 5, "synthetic", "inner nodes removed, empty unknown untouched");

        var trees = KeeperCore.DirectChildTrees(cuts[0], A);
        Check(trees.Count == 2 && trees[1].Count == 2, "synthetic", "loose trees: apple, and mod_box2 with bean");
        Check(KeeperCore.DirectChildUniqueIds(cuts[0], A).SequenceEqual(new[] { 102, 103 }), "synthetic", "direct child ids");

        JsonObject box = nodes.First(n => A.UniqueId(n) == 101);
        A.SetChildren(box, new List<long> { 99 });
        Check(!KeeperCore.Restore(nodes, cuts[0], A, _ => true), "synthetic", "refuses a container that is not empty");
        A.SetChildren(box, new List<long>());
        Check(!KeeperCore.Restore(nodes, cuts[0], A, n => !A.Id(n).StartsWith("mod_")), "synthetic", "refuses while still unknown");
        var wrongId = new Cut<JsonObject> { ParentUniqueId = 101, ParentId = "other", Nodes = cuts[0].Nodes };
        Check(!KeeperCore.Restore(nodes, wrongId, A, _ => true), "synthetic", "refuses a different item with the same unique id");
        Check(KeeperCore.Restore(nodes, cuts[0], A, _ => true), "synthetic", "restores once the mod is back");
        Check(Canonical(nodes) == before, "synthetic", "tree identical");

        // A cycle must not hang.
        List<JsonObject> cyc = new List<JsonObject> { N("save_bag", 0, 0, (1, 1)), N("mod_a", 1, 1, (2, 1)), N("x", 2, 2, (1, 1)) };
        var cc = KeeperCore.CutUnknownContents(cyc, A, n => !A.Id(n).StartsWith("mod_"), Clone);
        Check(cc.Count == 1 && cc[0].Nodes.Count == 2, "synthetic", "cycle cut terminates");
    }

    // ---- JSON node access -------------------------------------------------------------

    private sealed class Access : INodeAccess<JsonObject>
    {
        public string Id(JsonObject n) => n["identifier"]?.GetValue<string>() ?? "";
        public long Uuid(JsonObject n) => n["uuid"].GetValue<long>();
        public void SetUuid(JsonObject n, long u) => n["uuid"] = u;
        public int UniqueId(JsonObject n) => n["uniqueId"]?.GetValue<int>() ?? 0;
        public List<long> Children(JsonObject n) => (n["childItems"] as JsonArray)?.Select(x => x.GetValue<long>()).ToList() ?? new List<long>();
        public void SetChildren(JsonObject n, List<long> c) => n["childItems"] = new JsonArray(c.Select(x => (JsonNode)x).ToArray());
        public List<int> ChildInventories(JsonObject n) => (n["childItemInventoryNode"] as JsonArray)?.Select(x => x.GetValue<int>()).ToList() ?? new List<int>();
        public void SetChildInventories(JsonObject n, List<int> c) => n["childItemInventoryNode"] = new JsonArray(c.Select(x => (JsonNode)x).ToArray());
    }

    private static readonly Access A = new Access();

    private static JsonObject Clone(JsonObject n) => JsonNode.Parse(n.ToJsonString()).AsObject();

    private static List<JsonObject> Load(string json) =>
        JsonNode.Parse(json)["saveItems"].AsArray().Select(x => x.AsObject()).Select(Clone).ToList();

    private static string Save(List<JsonObject> nodes) =>
        new JsonObject { ["saveItems"] = new JsonArray(nodes.Select(n => (JsonNode)Clone(n)).ToArray()) }.ToJsonString();

    private static JsonObject N(string id, long uuid, int uniqueId, params (long child, int inv)[] kids) => new JsonObject
    {
        ["identifier"] = id,
        ["uuid"] = uuid,
        ["uniqueId"] = uniqueId,
        ["childItems"] = new JsonArray(kids.Select(k => (JsonNode)k.child).ToArray()),
        ["childItemInventoryNode"] = new JsonArray(kids.Select(k => (JsonNode)k.inv).ToArray()),
    };

    // The item tree from the root, independent of uuids: id#uniqueId, children by inventory.
    private static string Canonical(List<JsonObject> nodes)
    {
        Dictionary<long, JsonObject> by = nodes.ToDictionary(A.Uuid);
        string Walk(JsonObject n, int depth)
        {
            if (depth > 64)
                return "!deep";
            var kids = A.Children(n).Zip(A.ChildInventories(n), (c, inv) => (c, inv))
                .Select(k => k.inv + ":" + (by.TryGetValue(k.c, out JsonObject ch) ? Walk(ch, depth + 1) : "?missing"))
                .OrderBy(s => s, StringComparer.Ordinal);
            // Every field the game restores must survive, not just the structure.
            string payload = string.Join(",", n.Where(p => p.Key != "uuid" && p.Key != "childItems" && p.Key != "childItemInventoryNode")
                .OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "=" + p.Value?.ToJsonString()));
            return A.Id(n) + "#" + A.UniqueId(n) + "{" + payload.GetHashCode() + "}[" + string.Join(";", kids) + "]";
        }
        return Walk(nodes[0], 0);
    }

    private static bool NoDanglingRefs(List<JsonObject> nodes)
    {
        HashSet<long> uuids = nodes.Select(A.Uuid).ToHashSet();
        return nodes.All(n => A.Children(n).All(uuids.Contains) && A.Children(n).Count == A.ChildInventories(n).Count);
    }

    private static void Check(bool ok, string label, string what)
    {
        checks++;
        if (ok)
            return;
        failures++;
        Console.WriteLine($"FAIL {label}: {what}");
    }
}
