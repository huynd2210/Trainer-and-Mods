// Dump replay file stats - runs outside the game.
using System;
using System.Collections.Generic;
using CulticReplay;
using UnityEngine;

public static class DumpReplay
{
    public static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("usage: DumpReplay <file.cshrep>");
            return 1;
        }
        ReplayData d = ReplayFile.Read(args[0]);
        Console.WriteLine("format=v" + d.formatVersion + "  scene=" + d.sceneName + "  recordedAt=" + d.recordedAt);
        Console.WriteLine("frames=" + d.frames.Count + "  actors=" + d.actors.Count +
            "  audioEvents=" + d.audio.Count + "  effectEvents=" + d.effects.Count);
        Dictionary<string, int> effectTypes = new Dictionary<string, int>();
        foreach (EffectEvent effect in d.effects)
        {
            if (!effectTypes.ContainsKey(effect.prefab)) { effectTypes[effect.prefab] = 0; }
            effectTypes[effect.prefab]++;
        }
        foreach (KeyValuePair<string, int> effect in effectTypes)
        {
            Console.WriteLine("  effect type " + effect.Key + " x" + effect.Value);
        }
        float t0 = d.frames[0].t;
        float t1 = d.frames[d.frames.Count - 1].t;
        float st0 = d.frames[0].st;
        float st1 = d.frames[d.frames.Count - 1].st;
        Console.WriteLine("wall span=" + (t1 - t0).ToString("F1") + "s  game span=" + (st1 - st0).ToString("F1") + "s");

        int projDefs = 0; int playerDefs = 0; int layerless = 0; int goreDefs = 0;
        Dictionary<string, int> projTypes = new Dictionary<string, int>();
        foreach (ActorDef a in d.actors)
        {
            if (a.isProjectile)
            {
                projDefs++;
                if (!projTypes.ContainsKey(a.typeName)) { projTypes[a.typeName] = 0; }
                projTypes[a.typeName]++;
            }
            if (a.isPlayer) { playerDefs++; }
            if (a.layers.Count == 0 && !a.isProjectile) { layerless++; }
            if (a.typeName != null && a.typeName.StartsWith("prefabGib", StringComparison.OrdinalIgnoreCase))
            {
                goreDefs++;
            }
        }
        Console.WriteLine("projectile defs=" + projDefs + "  player defs=" + playerDefs +
            "  gore defs=" + goreDefs + "  non-proj layerless=" + layerless);
        foreach (KeyValuePair<string, int> kv in projTypes)
        {
            Console.WriteLine("  proj type " + kv.Key + " x" + kv.Value);
        }

        // timeScale distribution over frames.
        Dictionary<string, int> tsBuckets = new Dictionary<string, int>();
        int tracerSamples = 0; int dotSamples = 0; int visibleSamples = 0;
        int projectileSamples = 0;
        HashSet<int> projectileIds = new HashSet<int>();
        foreach (ActorDef a in d.actors) { if (a.isProjectile) { projectileIds.Add(a.id); } }
        foreach (ReplayFrame f in d.frames)
        {
            string bucket = f.ts <= 0.05f ? "<=0.05" : (f.ts < 0.95f ? "0.05-0.95" : ">=0.95");
            if (!tsBuckets.ContainsKey(bucket)) { tsBuckets[bucket] = 0; }
            tsBuckets[bucket]++;
            foreach (ActorFrame af in f.entries)
            {
                if (projectileIds.Contains(af.actorId) && af.layers.Count > 0)
                {
                    projectileSamples += af.layers.Count;
                }
                foreach (LayerState ls in af.layers)
                {
                    if (ls.spriteIdx < 0) { continue; }
                    visibleSamples++;
                    if (ls.spriteIdx < d.spriteTable.Count)
                    {
                        string n = d.spriteTable[ls.spriteIdx];
                        if (n == "___tracer___") { tracerSamples++; }
                        else if (n == "___dot___") { dotSamples++; }
                    }
                }
            }
        }
        Console.WriteLine("timeScale buckets:");
        foreach (KeyValuePair<string, int> kv in tsBuckets)
        {
            Console.WriteLine("  " + kv.Key + " : " + kv.Value + " frames");
        }
        Console.WriteLine("layer samples: visible=" + visibleSamples + "  tracer=" + tracerSamples + "  dot=" + dotSamples);
        Console.WriteLine("projectile layer samples=" + projectileSamples +
            (projDefs > 0 && projectileSamples == 0 ? "  WARNING: definitions exist but no projectile motion was stored" : ""));

        int withWeapon = 0;
        foreach (ReplayFrame f in d.frames) { if (f.wpSprite >= 0) { withWeapon++; } }
        Console.WriteLine("frames with weapon UI=" + withWeapon + " / " + d.frames.Count);
        return 0;
    }
}
