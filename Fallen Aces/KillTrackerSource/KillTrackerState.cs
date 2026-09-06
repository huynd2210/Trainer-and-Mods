using System;
using System.Collections.Generic;

namespace FallenAcesKillTracker
{
    internal enum TrackedDisposition
    {
        Active,
        Unconscious,
        Killed
    }

    internal sealed class KillTrackerState
    {
        internal sealed class CategoryCounts
        {
            public int Killed { get; set; }
            public int Unconscious { get; set; }
        }

        private sealed class ActorRecord
        {
            public string Category;
            public TrackedDisposition Disposition;
        }

        private readonly Dictionary<int, ActorRecord> actors = new Dictionary<int, ActorRecord>();
        private readonly SortedDictionary<string, CategoryCounts> categories =
            new SortedDictionary<string, CategoryCounts>(StringComparer.OrdinalIgnoreCase);

        public event Action Changed;

        public int Killed { get; private set; }
        public int Unconscious { get; private set; }
        public int ActorCount
        {
            get { return actors.Count; }
        }

        public SortedDictionary<string, CategoryCounts> Categories
        {
            get { return categories; }
        }

        public void MarkUnconscious(int actorId, string category)
        {
            ActorRecord actor;
            if (!actors.TryGetValue(actorId, out actor))
            {
                actor = new ActorRecord
                {
                    Category = CleanCategory(category),
                    Disposition = TrackedDisposition.Active
                };
                actors.Add(actorId, actor);
            }

            if (actor.Disposition == TrackedDisposition.Unconscious ||
                actor.Disposition == TrackedDisposition.Killed)
            {
                return;
            }

            actor.Disposition = TrackedDisposition.Unconscious;
            Unconscious++;
            GetCategory(actor.Category).Unconscious++;
            NotifyChanged();
        }

        public void MarkKilled(int actorId, string category)
        {
            ActorRecord actor;
            if (!actors.TryGetValue(actorId, out actor))
            {
                actor = new ActorRecord
                {
                    Category = CleanCategory(category),
                    Disposition = TrackedDisposition.Active
                };
                actors.Add(actorId, actor);
            }

            if (actor.Disposition == TrackedDisposition.Killed)
            {
                return;
            }

            if (actor.Disposition == TrackedDisposition.Unconscious)
            {
                Unconscious--;
                CategoryCounts previous = GetCategory(actor.Category);
                previous.Unconscious--;
                RemoveEmptyCategory(actor.Category, previous);
            }

            actor.Disposition = TrackedDisposition.Killed;
            Killed++;
            GetCategory(actor.Category).Killed++;
            NotifyChanged();
        }

        public void MarkAwake(int actorId)
        {
            ActorRecord actor;
            if (!actors.TryGetValue(actorId, out actor) || actor.Disposition == TrackedDisposition.Active)
            {
                return;
            }

            CategoryCounts counts = GetCategory(actor.Category);
            if (actor.Disposition == TrackedDisposition.Unconscious)
            {
                Unconscious--;
                counts.Unconscious--;
            }
            else
            {
                Killed--;
                counts.Killed--;
            }

            actor.Disposition = TrackedDisposition.Active;
            RemoveEmptyCategory(actor.Category, counts);
            NotifyChanged();
        }

        // Unity instance IDs are only meaningful within one loaded world.
        public void BeginWorld()
        {
            actors.Clear();
        }

        public void Restore(SortedDictionary<string, CategoryCounts> saved)
        {
            int killed = 0;
            int unconscious = 0;
            foreach (var pair in saved)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value.Killed < 0 || pair.Value.Unconscious < 0)
                    throw new System.IO.InvalidDataException("Invalid tracker counts.");
                checked { killed += pair.Value.Killed; unconscious += pair.Value.Unconscious; }
            }
            actors.Clear();
            categories.Clear();
            foreach (var pair in saved)
                categories.Add(pair.Key, new CategoryCounts { Killed = pair.Value.Killed, Unconscious = pair.Value.Unconscious });
            Killed = killed;
            Unconscious = unconscious;
        }

        private void NotifyChanged()
        {
            if (Changed != null) Changed();
        }

        public void Reset()
        {
            actors.Clear();
            categories.Clear();
            Killed = 0;
            Unconscious = 0;
        }

        private CategoryCounts GetCategory(string category)
        {
            CategoryCounts counts;
            if (!categories.TryGetValue(category, out counts))
            {
                counts = new CategoryCounts();
                categories.Add(category, counts);
            }
            return counts;
        }

        private void RemoveEmptyCategory(string category, CategoryCounts counts)
        {
            if (counts.Killed == 0 && counts.Unconscious == 0)
            {
                categories.Remove(category);
            }
        }

        private static string CleanCategory(string category)
        {
            return string.IsNullOrWhiteSpace(category) ? "Unknown" : category.Trim();
        }
    }
}
