using System;
using System.Collections.Generic;
using System.Linq;
using AssetRipper.Primitives;
using Iced.Intel;
using LibCpp2IL;

namespace Il2CppPeek
{
    /// <summary>
    /// Disassembles a method out of GameAssembly.dll.
    ///
    /// The interop assemblies BepInEx generates contain only stubs, so signatures are visible but
    /// behaviour is not. This loads the real IL2CPP binary, resolves a method to its compiled
    /// address, and disassembles it — which is how you find out what a function actually computes
    /// rather than inferring it by sampling.
    ///
    /// Usage: Il2CppPeek &lt;GameDir&gt; &lt;TypeName&gt; &lt;MethodName&gt; [instructionLimit]
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.Error.WriteLine("usage: Il2CppPeek <GameDir> <TypeName> <MethodName> [limit]");
                return 2;
            }

            var gameDir = args[0];
            var typeName = args[1];
            var methodName = args[2];
            var limit = args.Length > 3 && int.TryParse(args[3], out var l) ? l : 400;

            var binaryPath = System.IO.Path.Combine(gameDir, "GameAssembly.dll");
            var metadataPath = System.IO.Path.Combine(
                gameDir, "Swamp Keeper_Data", "il2cpp_data", "Metadata", "global-metadata.dat");

            LibCpp2IlMain.Settings.AllowManualMetadataAndCodeRegInput = false;

            var unityVersion = UnityVersion.Parse("2022.3.62f2");
            if (!LibCpp2IlMain.LoadFromFile(binaryPath, metadataPath, unityVersion))
            {
                Console.Error.WriteLine("Failed to load the IL2CPP binary/metadata.");
                return 1;
            }

            var metadata = LibCpp2IlMain.TheMetadata;
            var binary = LibCpp2IlMain.Binary;

            if(methodName=="unique-methods")
            {
                // IL2CPP folds empty/native-identical methods across unrelated types.
                // Detouring a shared empty body can hook core engine methods as well.
                var all=metadata.typeDefs.SelectMany(t=>t.Methods??Array.Empty<LibCpp2IL.Metadata.Il2CppMethodDefinition>()).Where(m=>m.MethodPointer!=0);
                var unique=all.GroupBy(m=>m.MethodPointer).Where(g=>g.Count()==1).Select(g=>g.First())
                    .Where(m=>m.DeclaringType.Name.StartsWith("Skill_") || m.DeclaringType.Name.StartsWith("Item_") || m.DeclaringType.Name.StartsWith("Enemy") || m.DeclaringType.Name.StartsWith("Boss") || m.DeclaringType.Name=="Player");
                var lines=unique.Select(m=>m.DeclaringType.Name+"."+m.Name).Distinct().OrderBy(s=>s);
                System.IO.File.WriteAllLines(args[3],lines);
                return 0;
            }

            if (methodName == "xref")
            {
                var all = metadata.typeDefs.SelectMany(t => t.Methods ?? Array.Empty<LibCpp2IL.Metadata.Il2CppMethodDefinition>()).Where(m => m.MethodPointer != 0).OrderBy(m => m.MethodPointer).ToArray();
                var target = all.First(m => m.DeclaringType.Name == typeName && m.Name == args[3]);
                var data = binary.GetRawBinaryContent();
                for (int i = 0; i < data.Length - 5; i++)
                {
                    if (data[i] != 0xE8) continue;
                    try {
                        var va = binary.MapRawAddressToVirtual((uint)i);
                        if ((long)va + 5 + BitConverter.ToInt32(data, i + 1) != (long)target.MethodPointer) continue;
                        var owner = all.LastOrDefault(m => m.MethodPointer <= va);
                        Console.WriteLine($"XREF {va:X} {owner?.DeclaringType.FullName}::{owner?.Name}");
                    } catch { }
                }
                return 0;
            }

            var types = metadata.typeDefs
                .Where(t => string.Equals(t.Name, typeName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (types.Count == 0)
            {
                Console.Error.WriteLine($"No type named '{typeName}'.");
                return 1;
            }

            var found = 0;
            foreach (var type in types)
            {
                foreach (var method in type.Methods ?? Array.Empty<LibCpp2IL.Metadata.Il2CppMethodDefinition>())
                {
                    if (methodName != "*" && !string.Equals(method.Name, methodName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    found++;
                    DumpMethod(binary, type.FullName, method, limit);
                }
            }

            if (found == 0)
            {
                Console.Error.WriteLine($"'{typeName}' has no method '{methodName}'. Available:");
                foreach (var m in types[0].Methods ?? Array.Empty<LibCpp2IL.Metadata.Il2CppMethodDefinition>())
                    Console.Error.WriteLine("  " + m.Name);
                return 1;
            }

            return 0;
        }

        /// <summary>
        /// Maps a call's target address back to the managed method compiled there, so the dump
        /// reads as names rather than addresses.
        /// </summary>
        private static string DescribeCallTarget(in Instruction instruction)
        {
            if (instruction.Mnemonic != Mnemonic.Call) return string.Empty;
            if (instruction.Op0Kind != OpKind.NearBranch64 && instruction.Op0Kind != OpKind.NearBranch32) return string.Empty;

            var target = instruction.NearBranch64;

            try
            {
                var methods = LibCpp2IlMain.GetManagedMethodImplementationsAtAddress(target);
                if (methods == null || methods.Count == 0) return string.Empty;

                var m = methods[0];
                var declaring = m.DeclaringType != null ? m.DeclaringType.Name : "?";
                var extra = methods.Count > 1 ? $" (+{methods.Count - 1} more)" : "";
                return $"        ; {declaring}::{m.Name}{extra}";
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static void DumpMethod(Il2CppBinary binary, string typeFullName,
                                       LibCpp2IL.Metadata.Il2CppMethodDefinition method, int limit)
        {
            var va = method.MethodPointer;
            Console.WriteLine();
            Console.WriteLine("================================================================");
            Console.WriteLine($"{typeFullName}::{method.Name}");
            Console.WriteLine($"  virtual address 0x{va:X}   rva 0x{method.Rva:X}   static={method.IsStatic}");
            Console.WriteLine("================================================================");

            if (va == 0)
            {
                Console.WriteLine("  (no compiled body — abstract, or stripped)");
                return;
            }

            if (!binary.TryMapVirtualAddressToRaw(va, out var rawOffset))
            {
                Console.WriteLine("  (could not map the address into the file)");
                return;
            }

            var image = binary.GetRawBinaryContent();
            var length = (int)Math.Min(limit * 16L, image.LongLength - rawOffset);
            if (length <= 0)
            {
                Console.WriteLine("  (address is past the end of the file)");
                return;
            }

            var code = new byte[length];
            Array.Copy(image, rawOffset, code, 0, length);

            var reader = new ByteArrayCodeReader(code);
            var decoder = Decoder.Create(32, reader, va);

            var formatter = new NasmFormatter();
            formatter.Options.DigitSeparator = "";
            formatter.Options.FirstOperandCharIndex = 10;
            formatter.Options.RipRelativeAddresses = true;

            var output = new StringOutput();
            var emitted = 0;
            var consecutivePadding = 0;
            var floatConstants = new List<(ulong Address, string Instruction)>();

            while (decoder.IP < va + (ulong)length && emitted < limit)
            {
                var instruction = decoder.Decode();
                if (instruction.IsInvalid) break;

                formatter.Format(instruction, output);
                var text = output.ToStringAndReset();

                // Resolving call targets back to managed names is what makes a dump readable —
                // otherwise every interesting branch is an opaque address.
                var annotation = DescribeCallTarget(instruction);
                Console.WriteLine($"  {instruction.IP:X16}  {text}{annotation}");
                emitted++;

                // Record rip-relative reads: these are where the compiler parked float constants.
                if (instruction.IsIPRelativeMemoryOperand)
                    floatConstants.Add((instruction.IPRelativeMemoryAddress, text));

                // A method has many `ret`s; stopping at the first one truncates it. Real function
                // boundaries are the int3 padding the compiler emits between them.
                if (instruction.Mnemonic == Mnemonic.Int3)
                {
                    if (++consecutivePadding >= 4) break;
                }
                else
                {
                    consecutivePadding = 0;
                }
            }

            if (floatConstants.Count == 0) return;

            Console.WriteLine();
            Console.WriteLine("  -- rip-relative constants referenced --");
            foreach (var (address, instruction) in floatConstants.Distinct())
            {
                if (!binary.TryMapVirtualAddressToRaw(address, out var constOffset)) continue;
                if (constOffset < 0 || constOffset + 8 > image.LongLength) continue;

                var f32 = BitConverter.ToSingle(image, (int)constOffset);
                var f64 = BitConverter.ToDouble(image, (int)constOffset);
                Console.WriteLine($"  0x{address:X}  float={f32}  double={f64}   [{instruction.Trim()}]");
            }
        }
    }
}
