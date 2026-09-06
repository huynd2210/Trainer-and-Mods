using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace FallenAcesKillTracker
{
    internal sealed class KillTrackerStore
    {
        private readonly string path;
        private bool writable;
        private bool recovered;

        public KillTrackerStore(string path) { this.path = path; }

        public void Load(KillTrackerState tracker)
        {
            writable = false;
            recovered = false;
            if (File.Exists(path))
            {
                try { tracker.Restore(Read(path)); }
                catch
                {
                    if (!File.Exists(path + ".bak")) throw;
                    tracker.Restore(Read(path + ".bak"));
                    recovered = true;
                }
            }
            else if (File.Exists(path + ".bak"))
            {
                tracker.Restore(Read(path + ".bak"));
                recovered = true;
            }
            writable = true;
        }

        private static SortedDictionary<string, KillTrackerState.CategoryCounts> Read(string filename)
        {
            var document = new XmlDocument { XmlResolver = null };
            using (var reader = XmlReader.Create(filename, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                document.Load(reader);
            var root = document.DocumentElement;
            if (root == null || root.Name != "KillTracker" || root.GetAttribute("version") != "1")
                throw new InvalidDataException("Unsupported tracker save.");
            var counts = new SortedDictionary<string, KillTrackerState.CategoryCounts>(StringComparer.OrdinalIgnoreCase);
            foreach (XmlNode node in root.ChildNodes)
            {
                var element = node as XmlElement;
                if (element == null) continue;
                if (element.Name != "Category") throw new InvalidDataException("Invalid tracker category.");
                counts.Add(element.GetAttribute("name"), new KillTrackerState.CategoryCounts {
                    Killed = XmlConvert.ToInt32(element.GetAttribute("killed")),
                    Unconscious = XmlConvert.ToInt32(element.GetAttribute("unconscious"))
                });
            }
            return counts;
        }

        public void Save(KillTrackerState tracker)
        {
            if (!writable) throw new IOException("Tracker save is protected because loading failed.");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temporary = path + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Indent = true, CloseOutput = false }))
                {
                    writer.WriteStartElement("KillTracker");
                    writer.WriteAttributeString("version", "1");
                    foreach (var pair in tracker.Categories)
                    {
                        writer.WriteStartElement("Category");
                        writer.WriteAttributeString("name", pair.Key);
                        writer.WriteAttributeString("killed", XmlConvert.ToString(pair.Value.Killed));
                        writer.WriteAttributeString("unconscious", XmlConvert.ToString(pair.Value.Unconscious));
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                }
                stream.Flush(true);
            }
            if (File.Exists(path))
                File.Replace(temporary, path, recovered ? null : path + ".bak");
            else
                File.Move(temporary, path);
            recovered = false;
        }
    }
}
