using API_LSP.Data;
using API_LSP.Models.DTOs;
using API_LSP.Utils;
using Microsoft.AspNetCore.Mvc;

namespace API_LSP.Controllers
{
    /// <summary>
    /// Area Image master for the image-based Paint Verification audit:
    /// image upload/CRUD + part hotspot mapping CRUD + the by-area read used
    /// by the audit screen. Images live in wwwroot/uploads/AreaImages/.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class MM_Area_Image_MasterController : BaseController
    {
        private const string UPLOAD_FOLDER = "AreaImages";

        private readonly MM_Area_ImageDataService _service;

        public MM_Area_Image_MasterController(
            MM_Area_ImageDataService service,
            IApiResponseHelper responseHelper,
            MM_ErrorLogDataService errorLogService)
            : base(responseHelper, errorLogService)
        {
            _service = service;
        }

        private static string UploadDir()
        {
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/", UPLOAD_FOLDER);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        // ─── Images ──────────────────────────────────────────────────────────

        [HttpGet("GetAllAreaImages")]
        public async Task<IActionResult> GetAllAreaImages()
        {
            try
            {
                var result = await _service.GetAllAreaImages();
                var ok = _responseHelper.CreateSuccessResponse(result, "Success", "Area images retrieved.");
                return Ok(ok);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "GetAllAreaImages");
            }
        }

        /// <summary>
        /// Create (Area_Image_ID = 0) or update an area image. Multipart form:
        /// metadata fields + optional File. File is required on create; on
        /// update it's optional (replaces the stored image when present).
        /// </summary>
        [HttpPost("SaveAreaImage")]
        public async Task<IActionResult> SaveAreaImage(
            [FromForm] decimal Area_Image_ID,
            [FromForm] decimal Vehicle_Area_ID,
            [FromForm] decimal Model_ID,
            [FromForm] string Image_Name,
            [FromForm] int SORTORDER,
            [FromForm] string? Inserted_Host,
            [FromForm] decimal? Inserted_User_ID,
            [FromForm] string? Plant_Code,
            [FromForm] IFormFile? File)
        {
            try
            {
                if (Vehicle_Area_ID <= 0 || Model_ID <= 0 || string.IsNullOrWhiteSpace(Image_Name))
                {
                    var bad = _responseHelper.CreateErrorResponse<decimal>(
                        "Validation Error", "Vehicle_Area_ID, Model_ID and Image_Name are required.");
                    return BadRequest(bad);
                }
                if (Area_Image_ID <= 0 && (File == null || File.Length == 0))
                {
                    var bad = _responseHelper.CreateErrorResponse<decimal>(
                        "Validation Error", "Image file is required when creating.");
                    return BadRequest(bad);
                }

                // Save the incoming file (if any) with a GUID prefix.
                string? savedName = null;
                if (File != null && File.Length > 0)
                {
                    savedName = $"{Guid.NewGuid()}_{Path.GetFileName(File.FileName)}";
                    var filePath = Path.Combine(UploadDir(), savedName);
                    await using var fs = new FileStream(filePath, FileMode.Create);
                    await File.CopyToAsync(fs);
                }

                var dto = new AreaImageSaveDto
                {
                    Area_Image_ID = Area_Image_ID,
                    Vehicle_Area_ID = Vehicle_Area_ID,
                    Model_ID = Model_ID,
                    Image_Name = Image_Name.Trim(),
                    SORTORDER = SORTORDER,
                    Inserted_Host = Inserted_Host,
                    Inserted_User_ID = Inserted_User_ID,
                    Plant_Code = Plant_Code,
                };

                decimal id;
                if (Area_Image_ID > 0)
                {
                    var oldFile = await _service.UpdateAreaImage(dto, savedName);
                    // A replacement upload succeeded — clean the orphaned old file.
                    if (oldFile != null && savedName != null)
                        TryDeleteFile(oldFile);
                    id = Area_Image_ID;
                }
                else
                {
                    id = await _service.CreateAreaImage(dto, savedName!);
                }

                var ok = _responseHelper.CreateSuccessResponse(id, "Success", "Area image saved.");
                return Ok(ok);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "SaveAreaImage");
            }
        }

        [HttpPost("DeleteAreaImage/{areaImageId}")]
        public async Task<IActionResult> DeleteAreaImage(decimal areaImageId)
        {
            try
            {
                if (areaImageId <= 0)
                {
                    var bad = _responseHelper.CreateErrorResponse<bool>(
                        "Validation Error", "Area_Image_ID is required.");
                    return BadRequest(bad);
                }

                // Soft delete — keep the file on disk since historical defects
                // may still reference this image for the future defect map.
                await _service.DeleteAreaImage(areaImageId);
                var ok = _responseHelper.CreateSuccessResponse(true, "Success", "Area image deleted.");
                return Ok(ok);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "DeleteAreaImage");
            }
        }

        /// <summary>Stream an area image by its stored file name.</summary>
        [HttpGet("GetImage")]
        public IActionResult GetImage([FromQuery] string fileName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fileName)
                    || fileName.Contains("..") || Path.IsPathRooted(fileName))
                {
                    var bad = _responseHelper.CreateErrorResponse<string>(
                        "Validation Error", "Invalid file name.");
                    return BadRequest(bad);
                }

                var filePath = Path.Combine(UploadDir(), fileName);
                if (!System.IO.File.Exists(filePath))
                    return NotFound();

                var ext = Path.GetExtension(fileName).ToLowerInvariant();
                var contentType = ext switch
                {
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    ".bmp" => "image/bmp",
                    ".webp" => "image/webp",
                    _ => "image/jpeg",
                };
                return PhysicalFile(filePath, contentType);
            }
            catch (Exception ex)
            {
                return HandleException(ex, "GetImage");
            }
        }

        // ─── Mappings ────────────────────────────────────────────────────────

        [HttpGet("GetMappings/{areaImageId}")]
        public async Task<IActionResult> GetMappings(decimal areaImageId)
        {
            try
            {
                var result = await _service.GetMappings(areaImageId);
                var ok = _responseHelper.CreateSuccessResponse(result, "Success", "Mappings retrieved.");
                return Ok(ok);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "GetMappings");
            }
        }

        [HttpPost("SaveMapping")]
        public async Task<IActionResult> SaveMapping([FromBody] AreaImageMappingDto dto)
        {
            try
            {
                if (dto == null || dto.Area_Image_ID <= 0 || dto.Part_ID <= 0
                    || string.IsNullOrWhiteSpace(dto.Shape))
                {
                    var bad = _responseHelper.CreateErrorResponse<decimal>(
                        "Validation Error", "Area_Image_ID, Part_ID and Shape are required.");
                    return BadRequest(bad);
                }

                var id = await _service.SaveMapping(dto);
                var ok = _responseHelper.CreateSuccessResponse(id, "Success", "Mapping saved.");
                return Ok(ok);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "SaveMapping");
            }
        }

        [HttpPost("DeleteMapping/{mappingId}")]
        public async Task<IActionResult> DeleteMapping(decimal mappingId)
        {
            try
            {
                if (mappingId <= 0)
                {
                    var bad = _responseHelper.CreateErrorResponse<bool>(
                        "Validation Error", "Mapping_ID is required.");
                    return BadRequest(bad);
                }

                var deleted = await _service.DeleteMapping(mappingId);
                var ok = _responseHelper.CreateSuccessResponse(deleted, "Success", "Mapping deleted.");
                return Ok(ok);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "DeleteMapping");
            }
        }

        // ─── Audit-screen read ───────────────────────────────────────────────

        /// <summary>All active images (with hotspots) for a Vehicle Area + Model —
        /// consumed by the Paint Verification stage. Images are model-specific.</summary>
        [HttpGet("GetImagesByArea/{vehicleAreaId}/{modelId}")]
        public async Task<IActionResult> GetImagesByArea(decimal vehicleAreaId, decimal modelId)
        {
            try
            {
                var result = await _service.GetImagesByArea(vehicleAreaId, modelId);
                var ok = _responseHelper.CreateSuccessResponse(result, "Success", "Area images retrieved.");
                return Ok(ok);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "GetImagesByArea");
            }
        }

        // ─── Helpers ─────────────────────────────────────────────────────────

        private static void TryDeleteFile(string fileName)
        {
            try
            {
                var path = Path.Combine(UploadDir(), fileName);
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }
            catch
            {
                // Orphaned file cleanup is best-effort — never fail the request.
            }
        }
    }
}
