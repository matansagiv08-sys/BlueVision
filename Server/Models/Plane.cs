using Server.DAL;

namespace Server.Models
{
    public class Plane
    {
        public int PlaneID { get; set; }
        public PlaneType Type { get; set; }
        public Project Project { get; set; }
        public int PriorityLevel { get; set; }
        public List<ItemInProduction> Items { get; set; } = new List<ItemInProduction>();

        public double Progress
        {
            get
            {
                if (Items == null || Items.Count == 0) return 0;
                double totalProgressSum = Items.Sum(i => i.Progress);
                return totalProgressSum / Items.Count;
            }
        }

        public Plane() { }
    }
}

