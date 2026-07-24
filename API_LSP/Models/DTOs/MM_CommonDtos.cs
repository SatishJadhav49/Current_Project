namespace API_LSP.Models.DTOs
{
   

    public class MM_ShopGetDTO
    {
        public decimal Shop_ID { get; set; }
        public string Shop_Name { get; set; } = String.Empty;
    }

    public class UserListDTO
    {
        public decimal Employee_ID { get; set; }
        public string Employee_Name { get; set; } = String.Empty;
    }

    public class MM_Audit_Area_Master
    {
        public decimal Audit_Area_ID { get; set; }
        public string Audit_Area_Name { get; set; } = String.Empty;
    }

    public class MM_Country_Master
    {
        public decimal Country_ID { get; set; }
        public string Country_Name { get; set; } = String.Empty;
    }

    public class MM_Build_Phase_Master
    {
        public decimal Build_Phase_ID { get; set; }
        public string Build_Phase_Name { get; set; } = String.Empty;
    }

    public class MM_Drive_Master
    {
        public decimal Drive_ID { get; set; }
        public string Drive_Name { get; set; } = String.Empty;
    }

    public class MM_Fuel_Master
    {
        public decimal Fuel_ID { get; set; }
        public string Fuel_Name { get; set; } = String.Empty;
    }

    public class MM_Plant_Master
    {
        public decimal Plant_ID { get; set; }
        public string Plant_Name { get; set; } = String.Empty;
        public string Plant_Code { get; set; } = String.Empty;
    }

    public class MM_Vehicle_Area_Master
    {
        public decimal Vehicle_Area_ID { get; set; }
        public string Vehicle_Area_Name { get; set; } = String.Empty;
    }

    public class MM_Severity_Master
    {
        public decimal Severity_ID { get; set; }
        public string Severity_Code { get; set; } = String.Empty;
        public string Severity_Name { get; set; } = String.Empty;
    }

}