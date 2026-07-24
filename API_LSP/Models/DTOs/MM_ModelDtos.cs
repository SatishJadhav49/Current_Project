using System.ComponentModel.DataAnnotations;

namespace API_LSP.Models.DTOs
{
    public class MM_ModelCreateDto
    {
        [Required]
        [StringLength(100)]
        public string Model_Code { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Model_Name { get; set; }

        public decimal? Variant_ID { get; set; }

        [StringLength(100)]
        public string? Model_Description { get; set; }

        [StringLength(30)]
        public string? PlatForm_Name { get; set; }

        [Required]
        public decimal Plant_ID { get; set; }

        public decimal? Shop_ID { get; set; }

        public decimal? Variant_Code { get; set; }

        public decimal? Sap_Code { get; set; }

        public bool? Is_Purgeable { get; set; }

        public decimal? Audit_Type_Id { get; set; }

        [StringLength(50)]
        public string? Vehicle_Type { get; set; }

        [StringLength(15)]
        public string? Plant_Code { get; set; }

        public string? Email_Address { get; set; }
    }

  

    public class MM_ModelListDto
    {
        public decimal Model_ID { get; set; }
        public string? Model_Name { get; set; }
        public bool? Is_LSP_Active { get; set; }
        public string? Updated_Host { get; set; }
        public decimal? Updated_User_ID { get; set; }
    }
}
