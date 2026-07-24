namespace API_LSP.Models.DTOs
{
    /// <summary>One uploaded area image row + its mapped-part count.</summary>
    public class AreaImageListDto
    {
        public decimal Area_Image_ID { get; set; }
        public decimal Vehicle_Area_ID { get; set; }
        public string Vehicle_Area_Name { get; set; } = string.Empty;
        public decimal Model_ID { get; set; }
        public string Model_Name { get; set; } = string.Empty;
        public string Image_Name { get; set; } = string.Empty;
        public string Image_FileName { get; set; } = string.Empty;
        public int SORTORDER { get; set; }
        public int Mapping_Count { get; set; }
    }

    /// <summary>Create/update payload for an area image (metadata only —
    /// the file itself arrives as multipart alongside these fields).</summary>
    public class AreaImageSaveDto
    {
        public decimal Area_Image_ID { get; set; }          // 0 = create
        public decimal Vehicle_Area_ID { get; set; }
        public decimal Model_ID { get; set; }
        public string Image_Name { get; set; } = string.Empty;
        public int SORTORDER { get; set; }
        public string? Inserted_Host { get; set; }
        public decimal? Inserted_User_ID { get; set; }
        public string? Plant_Code { get; set; }
    }

    /// <summary>One part hotspot on an image. Shape is JSON with normalized
    /// (0..1) coordinates, e.g. {"type":"rect","x":0.32,"y":0.55,"w":0.18,"h":0.12}.</summary>
    public class AreaImageMappingDto
    {
        public decimal Mapping_ID { get; set; }             // 0 = create
        public decimal Area_Image_ID { get; set; }
        public decimal Part_ID { get; set; }
        public string Part_Name { get; set; } = string.Empty;
        public string Shape { get; set; } = string.Empty;
        public string? Inserted_Host { get; set; }
        public decimal? Inserted_User_ID { get; set; }
        public string? Plant_Code { get; set; }
    }

    /// <summary>Image + its mappings — consumed by the Paint Verification
    /// audit screen after the auditor picks a Vehicle Area.</summary>
    public class AreaImageWithMappingsDto
    {
        public decimal Area_Image_ID { get; set; }
        public decimal Vehicle_Area_ID { get; set; }
        public decimal Model_ID { get; set; }
        public string Image_Name { get; set; } = string.Empty;
        public string Image_FileName { get; set; } = string.Empty;
        public int SORTORDER { get; set; }
        public List<AreaImageMappingDto> Mappings { get; set; } = new();
    }
}
