import {
  DTC,
  ECOS,
  LSP_AUDIT,
  PAINT_VERIFICATION,
  PHYSICAL_RFD,
  RECHECK,
  REWORK,
  ROAD_TEST,
  SHOWER_TEST,
  UNDERBODY,
  VMS,
} from '../../app.constants';

export class VehicleDetails {
  Vehicle_ID: number = 0;
  VIN_No: string = '';
  Body_Serial_No: string = '';
  Model_ID: number = 0;
  Drive_ID: number = 0;
  Fuel_ID: number = 0;
  Country_ID: number = 0;
  Build_Phase_ID: number = 0;
  Engine_No: string = '';
  Roll_Down_Date?: Date | string | null;
  CAI_OK_Date?: Date | string | null;
  Model_Code: string = '';
  Model_Desc: string = '';
  Variant?: string = '';
  Colour_Code?: string = '';
  Colour_Desc?: string = '';
  Is_Transfered?: boolean;
  Is_Purgeable?: boolean;
  Is_Edited?: boolean;
  Is_Deleted?: boolean;
  Inserted_Host?: string = localStorage.getItem('Hostname') ?? '';
  Inserted_User_ID?: number = Number(localStorage.getItem('Employee_ID')) || 0;
  Inserted_Date?: Date;
  Updated_Host?: string = localStorage.getItem('Hostname') ?? '';
  Updated_User_ID?: number = Number(localStorage.getItem('Employee_ID')) || 0;
  Updated_Date?: Date;
  Plant_Code?: string = localStorage.getItem('Plant_Code') ?? '';
}

export interface BuildPhase {
  Build_Phase_ID: number;
  Build_Phase_Name: string;
}

export interface DriveType {
  Drive_ID: number;
  Drive_Name: string;
}

export interface FuelType {
  Fuel_ID: number;
  Fuel_Name: string;
}

export interface CountryItem {
  Country_ID: number;
  Country_Name: string;
}


export interface ModelItem {
  Model_ID: number;
  Model_Name: string;
  /** Model's current build phase — auto-selected on Identify Vehicle. */
  Current_Build_Phase_ID?: number | null;
}

export interface PlantItem {
  Plant_ID: number;
  Plant_Name: string;
  Plant_Code: string;
}

export interface AuditArea {
  Audit_Area_ID: number;
  Audit_Area_Name: string;
}

export interface AuditorItem {
  Auditor_ID: number;
  Auditor_Name: string;
  Audit_Area_ID: number;
  Audit_Area_Name?: string;
}

/** Payload for per-row rework upsert. */
export interface ReworkUpsertPayload {
  Defect_Entry_ID: number;
  Rework_Done: boolean | null;
  Rework_By: string | null;
  Remark?: string | null;
  Inserted_Host?: string;
  Inserted_User_ID?: number;
  Plant_Code?: string;
}

/** Payload for stage-level rework upsert — used when DTC/ECOS/VMS/Paint were
 *  saved as NOK and later reworked. Keyed on (Vehicle_ID, Audit_Area_ID). */
export interface StageReworkUpsertPayload {
  Vehicle_ID: number;
  Audit_Area_ID: number;
  Rework_Done: boolean;
  Rework_By: string | null;
  Remark?: string | null;
  Documents?: string[];
  Inserted_Host?: string;
  Inserted_User_ID?: number;
  Plant_Code?: string;
}

/** Payload for OK/NOK-style stages (DTC / ECOS / VMS) — no concerns,
 *  just status + documents. */
export interface StageStatusPayload {
  Vehicle_ID: number;
  Audit_Area_ID: number;
  Auditor_Name: string;
  Is_No_Defect: boolean;
  Documents: string[];
  Inserted_Host?: string;
  Inserted_User_ID?: number;
  Plant_Code?: string;
}

/** Static stage IDs from MM_Audit_Area_Master (rarely change). */
export const STAGE = {
  IDENTIFY: 0,
  ROAD_TEST: ROAD_TEST,
  UNDERBODY: UNDERBODY,
  DTC: DTC,
  SHOWER_TEST: SHOWER_TEST,
  ECOS: ECOS,
  LSP_AUDIT: LSP_AUDIT,
  VMS: VMS,
  RECHECK: RECHECK,
  PAINT_VERIFICATION: PAINT_VERIFICATION,
  REWORK: REWORK,
  PHYSICAL_RFD: PHYSICAL_RFD,
} as const;

export type StageStatus = 'pending' | 'active' | 'completed';

export interface StageView {
  Audit_Area_ID: number;
  Audit_Area_Name: string;
  status: StageStatus;
}

// ───────────────── Master lookups used by defect-entry-form ─────────────────

export interface VehicleAreaItem {
  Vehicle_Area_ID: number;
  Vehicle_Area_Name: string;
}

export interface SeverityItem {
  Severity_ID: number;
  Severity_Code: string;
  Severity_Name: string;
}

export interface PartItem {
  Part_ID: number;
  Part_Name: string;
}

export interface DefectItem {
  Defect_ID: number;
  Defect_Name: string;
}

export interface LocationItem {
  Location_ID: number;
  Location_Name: string;
}

export interface AttributionItem {
  Attribution_ID: number;
  Attribution_Name: string;
}

export interface DefectTypeItem {
  Defect_Type_ID: number;
  Defect_Type_Name: string;
}

// ───────────────── A single in-memory concern row (UI side) ─────────────────

export interface ConcernRow {
  /** Populated after server save; absent for rows not yet persisted. */
  Defect_Entry_ID?: number;
  /** Original stage this concern belongs to — populated when the row was
   *  loaded by a cross-stage view (Recheck). */
  Audit_Area_ID?: number;
  Audit_Area_Name?: string;
  Vehicle_Area_ID?: number;
  Vehicle_Area_Name?: string;
  Part_ID: number;
  Part_Name: string;
  Defect_ID: number;
  Defect_Name: string;
  Location_ID: number;
  Location_Name: string;
  Severity_ID: number;
  Severity_Code: string;
  Attribution_ID: number;
  Attribution_Name: string;
  Defect_Type_ID: number;
  Defect_Type_Name: string;
  Remark?: string;
  /** When true, the concern's rework has been completed; edits and deletes
   *  are forbidden anywhere in the UI (and rejected by the API). */
  Rework_Done?: boolean;
  /** Image click-point (Paint Verification) — normalized 0..1. */
  Area_Image_ID?: number | null;
  Image_X?: number | null;
  Image_Y?: number | null;
}

// ───────────────── Payloads for stage save ─────────────────

export interface VehicleDefectEntryPayload {
  Vehicle_Area_ID?: number | null;
  Part_ID: number;
  Defect_ID: number;
  Location_ID: number;
  Severity_ID?: number | null;
  Attribution_ID?: number | null;
  Defect_Type_ID?: number | null;
  Remark?: string | null;
  /** Image click-point (Paint Verification only) — normalized 0..1. */
  Area_Image_ID?: number | null;
  Image_X?: number | null;
  Image_Y?: number | null;
}

// ───────────────── Area images (image-based Paint audit) ─────────────────

export interface AreaImageMapping {
  Mapping_ID: number;
  Area_Image_ID: number;
  Part_ID: number;
  Part_Name: string;
  /** JSON: {"type":"rect","x":…,"y":…,"w":…,"h":…} — normalized 0..1. */
  Shape: string;
}

export interface AreaImageWithMappings {
  Area_Image_ID: number;
  Vehicle_Area_ID: number;
  Model_ID: number;
  Image_Name: string;
  Image_FileName: string;
  SORTORDER: number;
  Mappings: AreaImageMapping[];
}

export interface StageSavePayload {
  Vehicle_ID: number;
  Audit_Area_ID: number;
  Auditor_Name: string;
  Is_No_Defect: boolean;
  Documents?: string | null;
  Inserted_Host?: string;
  Inserted_User_ID?: number;
  Plant_Code?: string;
  Concerns: VehicleDefectEntryPayload[];
}

// ───────────────── Pending rework list (standalone Rework feature) ─────────────────

/** One row from MM_Vehicle_Tracking/GetPendingReworkList. Mirrors a
 *  ConcernRow plus the parent vehicle's VIN + the existing rework state. */
export interface PendingReworkItem {
  Vehicle_ID: number;
  VIN_No: string;
  Defect_Entry_ID: number;
  Audit_Area_ID: number;
  Audit_Area_Name: string;
  Vehicle_Area_ID?: number | null;
  Vehicle_Area_Name?: string;
  Part_ID: number;
  Part_Name: string;
  Defect_ID: number;
  Defect_Name: string;
  Location_ID: number;
  Location_Name: string;
  Severity_ID?: number | null;
  Severity_Code?: string;
  Attribution_ID?: number | null;
  Attribution_Name?: string;
  Defect_Type_ID?: number | null;
  Defect_Type_Name?: string;
  Remark?: string | null;
  Rework_ID?: number | null;
  Rework_Done?: boolean | null;
  Rework_By?: string;
  Rework_Date?: string | null;
}

// ───────────────── Shower-test import payloads ─────────────────

export interface ShowerImportRequest {
  Vehicle_ID: number;
  VIN_No: string;
  Inserted_Host?: string;
  Inserted_User_ID?: number;
  Plant_Code?: string;
}

export interface ShowerSkippedRow {
  Part_Name?: string | null;
  MissingMaster: string;
  MissingValue: string;
}

export interface ShowerImportResult {
  Found: boolean;
  Is_No_Defect: boolean;
  Auditor_Name: string;
  Audit_Date?: string | null;
  Imported: number;
  Skipped: ShowerSkippedRow[];
}
