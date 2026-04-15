namespace Server.Models
{
    //DTO that contains all data required to initialize the "Add Item to Production" form(dropdowns and initial values)
    public class ItemsInProductionInitialFormData
    {
        public List<ProductionItem> ProductionItems { get; set; } = new List<ProductionItem>();
        public List<Project> Projects { get; set; } = new List<Project>();
        public List<PlaneType> PlaneTypes { get; set; } = new List<PlaneType>();
        public List<int> ExistingWorkOrders { get; set; } = new List<int>();
        public List<PriorityOption> Priorities { get; set; } = new List<PriorityOption>();
        public List<PlaneOption> Planes { get; set; } = new List<PlaneOption>();
    }

    public class PriorityOption
    {
        public int ID { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class PlaneOption
    {
        public string PlaneID { get; set; } = string.Empty;
        public int TypeID { get; set; }
        public int ProjectID { get; set; }
    }
}
