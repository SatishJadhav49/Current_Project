using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_LSP.Models.DTOs
{
    public class AttributionMasterCreateDto
    {
        public decimal Attribution_ID { get; set; }
        public string Attribution_Name { get; set; } = string.Empty;
        public int SORTORDER { get; set; }
        public string? Inserted_Host { get; set; } = string.Empty;
        public decimal? Inserted_User_ID { get; set; }
        public DateTime? Inserted_Date { get; set; }
        public string? Plant_Code { get; set; } = string.Empty;

    }

    public class AttributionMasterUpdateDto
    {
        public decimal Attribution_ID { get; set; }
        public string Attribution_Name { get; set; } = string.Empty;
        public int SORTORDER { get; set; }
        public string Updated_Host { get; set; } = string.Empty;
        public decimal Updated_User_ID { get; set; }
        public Nullable<System.DateTime> Inserted_Date { get; set; }
        public string Plant_Code { get; set; } = string.Empty;


    }

    public class AttributionMasterListDto
    {
        public decimal Attribution_ID { get; set; }
        public string Attribution_Name { get; set; } = string.Empty;
        public int SORTORDER { get; set; }
        public string Inserted_Host { get; set; } = string.Empty;
        public decimal Inserted_User_ID { get; set; }
        public Nullable<System.DateTime> Inserted_Date { get; set; }
        public string Plant_Code { get; set; } = string.Empty;
    }
    
    
    

}