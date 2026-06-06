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

        public object CreatePlane(CreatePlaneRequest? data)
        {
            string planeID = data?.PlaneID?.Trim() ?? string.Empty;
            int projectID = data?.ProjectID ?? 0;
            int planeTypeID = data?.PlaneTypeID ?? 0;

            if (string.IsNullOrWhiteSpace(planeID))
            {
                throw new Exception("Plane identifier is required.");
            }

            if (projectID <= 0)
            {
                throw new Exception("Project is required.");
            }

            if (planeTypeID <= 0)
            {
                throw new Exception("Plane type is required.");
            }

            DBservices dbs = new DBservices();
            return dbs.CreatePlaneForProject(projectID, planeID, planeTypeID);
        }
    }

    public class CreatePlaneRequest
    {
        public int? ProjectID { get; set; }
        public string? PlaneID { get; set; }
        public int? PlaneTypeID { get; set; }
    }
}
