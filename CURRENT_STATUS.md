# LSP Digitization — Project Handbook for Claude sessions

**Read this whole file first.** It contains everything needed to work on this
project without re-exploring: architecture, every stage's logic, DB schema,
endpoint list, conventions, and the user's working style. Trust this file over
guesses; verify against code only when something looks stale.

---

## 1. What this project is

**Mahindra Launch Sampling Plan (LSP) digitization.** PQA's manual paper
process for auditing vehicle build quality during launch phases, replaced by a
web app. Operators on the **assembly line** scan a VIN and walk the vehicle
through ~11 audit stages, capturing concerns, files, and OK/NOK statuses. The
final stage marks **Physical RFD (Ready For Dispatch)**.

Audience = line workers (~12th-grade education). UX rules:
- Big tap targets (≥48px), large fonts, minimal jargon, one-column flow.
- Tablets primarily; desktop fine.

### Reference documents
- `/Users/satishjadhav/Desktop/LSP Digitalization.pptx` — original requirements
  (read via `python3 -m markitdown "<path>"`). Slide 22 = global rules.
- `/Users/satishjadhav/Downloads/LSP.sql` — full SQL Server schema dump for
  `PQ_LSP`. UTF-16 encoded — Read with offset/limit, never `cat`.

---

## 2. Repo layout + how to run

```
/Users/satishjadhav/Desktop/LSP
├── API/                     # .NET 8, raw ADO.NET (no EF)
│   ├── Controllers/         # one per feature; BaseController has error handling
│   ├── Data/                # *DataService classes — all SQL lives here
│   ├── Models/DTOs/
│   ├── SqlScripts/          # idempotent migrations (see §10)
│   ├── wwwroot/uploads/{folder}/   # uploaded files, GUID-prefixed names
│   ├── appsettings.json            # PROD conn + Shower conn + SMTP
│   └── appsettings.Development.json  # localhost conn — MUST keep
│                                      # MultipleActiveResultSets=true
├── UI/                      # Angular 18 standalone + Tailwind
│   └── src/app/
│       ├── Features/
│       │   ├── Masters/     # attribution, auditor, defect, defecttype,
│       │   │                # location, model, part, areaimage — CRUD pages
│       │   ├── Rework/      # standalone cross-vehicle rework page (§7)
│       │   ├── VehicleTracking/   # the core feature (§4–6)
│       │   ├── UserManagement/, Documents/, Dashboard/
│       ├── Shared/          # confirmation-modal, toast-container, models
│       ├── auth/            # AuthGuard, MenuAccessGuard, MenuService
│       ├── config/app.config.service.ts   # baseApiPath
│       └── services/        # api-request, toast, confirmation
└── CURRENT_STATUS.md        # this file
```

**Run**: API `cd API && dotnet build && dotnet run` (:5000).
UI `cd UI && npm start` (:4200).
**Typecheck**: `cd UI && npx tsc --noEmit -p tsconfig.app.json` (must stay clean).
**Backend build**: `cd API && dotnet build` (0 errors; ~13 nullable warnings pre-existing — don't add more).

### Auth basics (rarely touched)
Login via `MM_Employee`. Frontend stashes in localStorage: `Plant_ID`,
`Plant_Code` (seed = `A003`), `Employee_ID`, `Hostname`, `token`/`user`.
Every write includes `Inserted_Host`, `Inserted_User_ID`, `Plant_Code`.
Sidebar menus are DB-driven (`MM_Menus` etc.) — **the user adds menu rows
manually in SQL**; new pages just need a route.

### THE api-request footgun (memorize this)
- `apiRequest.get(url)` and `postForGet(url, body)` → return `response.datalist` (the payload).
- `apiRequest.post(url, body)` → returns `response.res` (metadata only).
- **Use `postForGet` whenever the response body matters** (new IDs, results).
  `postForGet` also handles `FormData` (skips Content-Type) for file uploads.

---

## 3. Stage IDs and flow

`UI/src/app/app.constants.ts` + `STAGE` map in `vehicle-tracking.models.ts`:

| ID | Stage | Type |
|---|---|---|
| 0 | Vehicle (Identify) | form save |
| 1 | Road Test | defect entry + Start/End KM auto-save |
| 2 | Underbody | defect entry |
| 3 | DTC | OK/NOK + files |
| 4 | Shower Test | **external import** (optional stage) |
| 5 | ECOS | OK/NOK + files (info-only for RFD) |
| 6 | Static Audit | defect entry (**UI label only** — folder/component/backend key still `lsp-audit`/`LspAudit`) |
| 7 | VMS | OK/NOK + files (required OK for RFD) |
| 8 | Recheck | cross-stage review |
| 9 | Rework | per-concern + stage-level rework |
| 11 | Physical RFD | readiness gate + vehicle lock |
| 12 | Paint Verification | **image-based defect entry** (§6) |

**Visual order comes from `MM_Audit_Area_Master.SORTORDER`, keys from ID.**
The user re-orders stages by changing SORTORDER in DB (e.g., may move Paint
before Recheck). Never assume ID order = display order.

Progress bar dots show **emoji icons** (`stageIcon()` in the shell):
🚗 🛣️ 🔩 💻 🚿 ⚡ 📋 🖥️ 🔁 🎨 🛠️ 🚚. Completed = ✓, skipped = `!`.
Stage headers show the same emoji in a **blue-outlined circle (no fill)**.

**Auto-navigation rules** (shell `loadStageLogs`):
- RFD done → land directly on Physical RFD.
- Else → first pending stage that is **not Shower Test** (optional, external).
- Manual click can still open any accessible stage, including Shower.

---

## 4. The shell + shared architecture

`Features/VehicleTracking/vehicle-tracking-home/` owns: VIN scan, `vehicle`,
`stages`, `currentStageId`, `rfdDone`, `recheckDone`.

**Stage component contract** (never break):
```ts
@Input() vehicle: VehicleDetails | null;
@Input() locked = false;              // whole-vehicle RFD lock
@Output() stageSaved = new EventEmitter<{auditAreaId, noDefect}>();
```
Every stage: header with emoji circle + `Vehicle: VIN` + `Saved: <logDate>`
timestamp (updated locally after each save, loaded from Track_Logs.Log_Date).

### The locks (heart of the architecture)
1. **Per-concern rework lock** — `MM_Vehicle_Defect_Rework.Rework_Done = 1`.
   Server: `IsConcernLockedByRework` guards UpdateConcern/DeleteConcern.
   UI: row 🔒, edit/delete disabled. Surfaced via `Rework_Done` LEFT JOIN.
2. **Whole-vehicle RFD lock** — RFD Track_Logs row exists (`Audit_Area_ID=11`).
   Server: `IsVehicleRfdDone` + `RfdLockedResponse<T>()` (400 "Locked") on
   EVERY write endpoint except SaveRfd. UI: amber banner, `[locked]` to all
   stages, `rfdDone` flips **instantly** in `onStageSaved(PHYSICAL_RFD)` (no
   round-trip wait). RFD page shows "PHYSICAL RFD DONE + timestamp" stamp
   instead of READY badge after dispatch.
3. **Recheck freeze** — `recheckDone` (Track_Logs row for stage 8) hides the
   Shower "Refresh from Booth" button; also flipped instantly on save.

### File uploads
`POST MM_Vehicle_Tracking/UploadStageFile` (multipart File+Folder) →
`wwwroot/uploads/{folder}/{guid}_{name}`. Folder whitelist in
`MM_Vehicle_TrackingController.AllowedFolders`:
`DTCData, ECOSData, VMSData, PaintData, StageData, StageReworkData`.
Area images use their own controller/folder (`AreaImages`).
Download: `GET DownloadStageFile?folder=&fileName=`.
Attachment viewing UX = **paperclip icon + count → popup modal with thumbnails**
(pattern lives in Recheck + Rework). The user rejects bare download links.

### defect-entry-form (shared picker)
`Features/VehicleTracking/shared/defect-entry-form/` — column-picker concern
table. Inputs: `[concerns] [saving] [pickerEnabled] [showStageColumn] [locked]
[partFilterIds]` (last one filters the Part column, used by Paint). Outputs:
`(addRequest) (updateRequest) (removeRequest)`. Public method
`presetSelection({Part_ID, Part_Name, Vehicle_Area_ID, Vehicle_Area_Name})`
lets a parent preselect (Paint image taps use it).

---

## 5. Stage-by-stage specifics

### Identify Vehicle (0)
VIN lookup DB-first then SP fallback. **`GetVinDetails` is STUBBED** for local
dev (overwrites display fields with mock values; the Drona SP
`SP_GetDataFrom_Vin_Number` call is commented out). Never revert without asking.
Save toast says "Updated" vs "Saved" based on `Vehicle_ID > 0` (user's tweak).

### Road Test (1), Underbody (2), Static Audit (6)
Defect-entry stages: auditor dropdown (auto-saves via `UpdateAuditor`),
"No Defect" button (only when 0 concerns), per-row Add/Update/Delete via
`AddConcern`/`UpdateConcern/{id}`/`DeleteConcern/{id}`. RT also has Start/End
KM inputs auto-saved via `UpdateRoadTestKm`.

### DTC (3), ECOS (5), VMS (7)
OK/NOK toggle + auditor + multi-file upload (drag-drop + paste) + Save via
`SaveStageStatus` (upserts Track_Logs with Is_No_Defect + Documents JSON).

### Shower Test (4) — external import
- Second DB connection `Shower_Test_Connection` (PQ_Dock_Audit) in
  appsettings; `IDbConnectionFactory.CreateShowerConnectionAsync()`.
- `POST MM_Shower_Test/ImportForVehicle {Vehicle_ID, VIN_No}` runs
  `SP_Get_Shower_Defects_For_LSP @Vin_No`, maps master names→IDs, upserts
  Track_Logs (Log_Date = Audit_Date) + replaces stage-4 defects.
  **Part auto-inserts into MM_Part_Master if missing; all other masters are
  lookup-only** — misses go into a `Skipped` list shown as a warning banner.
- Component behavior: first open loads cached Track_Logs; if none, imports.
  Manual "⟳ Refresh from Booth" re-imports (hidden after Recheck done).
  States: loading / not-done / rfd-locked-no-data / no-defect / with-defects.
- No data from SP → "Shower test is not yet done" card. RFD-locked with no
  data → "Physical RFD already done" card (no doomed import call).

### Recheck (8)
- **Stage Status table** = ALL audited stages (1,2,3,5,6,7,12) from
  `GetRecheckData.StatusStages` — auditor, OK/NOK/Pending chip, saved date,
  files (paperclip popup).
- All concerns across stages listed via defect-entry-form
  (`pickerEnabled=false, showStageColumn=true`) — edit/delete only.
- "Verify & Save" writes its own Track_Logs row via SaveStageStatus.

### Paint Verification (12) — image-based defect entry (§6 for masters side)
Defect-entry stage (same skeleton as Static Audit) **plus Image Audit panel**:
- Area dropdown (all vehicle areas) → `GET MM_Area_Image_Master/GetImagesByArea/{id}`
  loads images with hotspot mappings; tabs when multiple views.
- **Tap image** → normalized 0..1 point → hit-test rect hotspots
  (smallest-area-wins on overlap):
  - exactly 1 part → auto-select in defect form (still editable), hint strip.
  - multiple parts → `partFilterIds` shortlist + "Show all parts" escape.
  - no hit → all parts.
- Tap point (`pendingClick`) is saved on the concern
  (`Area_Image_ID, Image_X, Image_Y` — nullable cols on MM_Vehicle_Defects);
  cleared after add. Saved concerns render as **red pins** on the image;
  pending tap = green marker. SVG overlay `viewBox="0 0 1000 1000"
  preserveAspectRatio="none"` → coords × 1000.
- Areas without images degrade to plain defect entry.
- Attachments kept (folder `PaintData`) — auto-persisted via
  `POST UpdateStageDocuments` (Documents-only upsert, doesn't touch Is_No_Defect).
- **Future**: image heat-map report (which image regions get most defects) —
  the stored click coords exist exactly for this. User plans "major changes"
  to Paint later.

### Rework (9)
Two sections:
1. **Stage Failures** — NOK status stages (DTC/ECOS/VMS only; Paint excluded
   since its defects are normal concerns). One row per failed stage: auditor,
   failed-on date, auditor's NOK files (paperclip popup), rework status.
   "🛠 Rework" opens a modal: Done checkbox + Rework By (datalist + free text)
   + multi-line "What was done" remark + **attachments** (folder
   `StageReworkData`). All three required to save →
   `POST UpsertStageRework` (upsert on Vehicle_ID×Audit_Area_ID →
   `MM_Vehicle_Stage_Rework`).
2. **Concerns to rework** — every concern with its rework state. "🛠 Rework"/
   "✎ Edit" button opens a modal: Done + Rework By + remark (all required) →
   `UpsertRework` (per Defect_Entry_ID → `MM_Vehicle_Defect_Rework`).
No Track_Logs row for stage 9 — `GetStageLogsByVehicle` synthesizes a virtual
completion row when all concerns reworked. Header shows "Last rework: <max date>".
**Rework_By is free text everywhere** (nvarchar(100), datalist suggestions from
`MM_Auditor_Master` area 9, manual names allowed).

### Physical RFD (11)
- Readiness check table (`GET GetRfdReadiness/{vehicleId}`):
  - Road Test / Underbody / Static Audit / **Paint Verification** → completed-check.
  - DTC / VMS → must be OK **or** (NOK + StageReworkDone) → shows green ✓ +
    "✓ Reworked · name" chip (hover = remark).
  - ECOS → info-only (never gates), same reworked chip.
  - "All concerns reworked" → `Rework.PendingCount == 0` (includes Paint concerns).
- Exact server rule (`GetRfdReadiness`):
  `Ready = RT.Completed && UB.Completed && LspAudit.Completed && Paint.Completed
  && StagePassed(Dtc) && StagePassed(Vms) && PendingCount==0`
  where `StagePassed(s) = s.IsOk==true || (s.IsOk==false && s.StageReworkDone)`.
- Below readiness: **"Concerns reworked"** table (who/when per concern) and
  **"Concerns blocking dispatch"** (pending ones).
- Button: **"✓ Physical RFD Done"** → confirm modal → `SaveRfd` (server
  re-checks readiness) → flips `locked` + `alreadyDispatched` + emits →
  shell flips `rfdDone` instantly. Post-dispatch UI shows the
  "PHYSICAL RFD DONE + timestamp" stamp.
- **Future: sticker print** — decided to WAIT until printer hardware exists.
  Spec when ready: 5cm×2.5cm label, QR (VIN only) left, text right
  ("RFD OK", auditor name, RFD date-time), auto-print (no manual click) from
  backend as raw ZPL/TSPL over TCP:9100 on the SaveRfd success path;
  printer config to live in appsettings like SMTP. Do NOT build browser-print.

---

## 6. Area Image Master (masters page for Paint)

Route `/master/areaimagemaster` → `Features/Masters/areaimage/`.
- List: thumbnail, vehicle area, image name, sort, mapped-part count,
  actions (🎯 Map Parts / Edit / Delete-soft).
- Create/edit form: area dropdown + name (e.g., "Front View") + sort + file
  (required on create, optional replacement on edit). Multiple images per area.
- **Mapping editor**: pick a part → drag a rectangle on the image → hotspot
  saved. Boxes render with part labels; side list with hover-highlight +
  delete. Min drag size 1.5% to ignore accidental clicks.
- **Coordinates are normalized 0..1 fractions** of image dimensions, stored as
  `Shape` JSON `{"type":"rect","x":0.32,"y":0.55,"w":0.18,"h":0.12}` —
  resolution-independent by design; schema is polygon-ready via `type`.
- Backend `MM_Area_Image_MasterController`: GetAllAreaImages, SaveAreaImage
  (multipart), DeleteAreaImage (soft — file kept for historical defect refs),
  GetImage?fileName= (streamer with path-traversal guard), GetMappings/{id},
  SaveMapping, DeleteMapping, GetImagesByArea/{vehicleAreaId}.
- Frontend service methods on `MasterService` (masters share one service).

---

## 7. Standalone Rework page (`/rework`)

`Features/Rework/rework.component` — for rework TEAMS (different from the
in-vehicle Rework stage). Cross-vehicle pending list:
- `GET MM_Vehicle_Tracking/GetPendingReworkList?attributionId=&vinNo=` —
  every concern with rework not done, across all vehicles; excludes
  RFD-locked vehicles. Filters: Attribution (default **All**), VIN substring
  (full VIN or last 8 chars — VIN shown as `…XXXXXXXX`, full on hover).
- Done rows are hidden (future: option to view done list).
- Per-row "🛠 Rework" opens the same modal pattern: Done + Rework By
  (datalist/free text) + required remark → `UpsertRework` → row disappears.
- Styling mirrors the in-stage Rework page (stage-style header card,
  blue circle 9, orange "Concerns to rework" heading).

---

## 8. Database schema (PQ_LSP)

| Table | Purpose |
|---|---|
| `MM_Vehicle_Details` | vehicle master; VINs; FK to Model/Drive/Fuel/Country/Build_Phase |
| `MM_Vehicle_Track_Logs` | ONE row per (Vehicle × Audit_Area) — UNIQUE. Auditor_Name, Is_No_Defect, Log_Date, Documents (JSON array string), Start_KM/End_KM (RT) |
| `MM_Vehicle_Defects` | one row per concern. FKs to masters + **Area_Image_ID, Image_X, Image_Y** (nullable, Paint click points) |
| `MM_Vehicle_Defect_Rework` | per-concern rework. UNIQUE(Defect_Entry_ID), FK cascade. Rework_Done/By/Date/Remark |
| `MM_Vehicle_Stage_Rework` | stage-level rework (DTC/ECOS/VMS NOK resolution). UNIQUE(Vehicle_ID, Audit_Area_ID). + Documents JSON |
| `MM_Area_Image_Master` | Paint audit images. FK Vehicle_Area, Image_Name, Image_FileName, soft-delete |
| `MM_Area_Image_Part_Mapping` | hotspots: FK Area_Image (cascade), Part_ID, Shape JSON |
| `MM_Audit_Area_Master` | stage rows — SORTORDER drives visual order |
| `MM_Vehicle_Area_Master`, `MM_Severity_Master` | seeded masters (no admin UI yet) |
| `MM_Auditor_Master` | auditors with Audit_Area_ID — filtered per stage |
| `MM_Part/Defect/Location/Attribution/Defect_Type_Master` | picker masters |
| `MM_Employee/Roles/User_Roles/Menus/Menu_Role/Sub_Menus` | auth/RBAC — don't touch |
| `SP_GetDataFrom_Vin_Number` | Drona SP for VIN lookup (stubbed locally) |
| external: `PQ_Dock_Audit..SP_Get_Shower_Defects_For_LSP` | shower data source |

**Conventions**: auditor/rework names are DENORMALIZED text (never FK).
Always UPSERT Track_Logs (unique constraint). Documents = JSON array string.

---

## 9. Backend endpoint cheatsheet

`MM_Vehicle_Tracking` (all writes RFD-lock-guarded except SaveRfd):
- Reads: `GetStageData/{vid}/{areaId}` (TrackLog+Concerns incl. coords+Rework_Done),
  `GetStageLogsByVehicle/{vid}`, `GetRecheckData/{vid}` (Concerns+StatusStages all stages),
  `GetReworkData/{vid}` (Concerns+StageFailures), `GetPendingReworkList?attributionId=&vinNo=`,
  `GetRfdReadiness/{vid}`, `DownloadStageFile?folder=&fileName=`
- Writes: `SaveStage`, `SaveStageStatus`, `SaveNoDefect`, `AddConcern`,
  `UpdateConcern/{id}`, `DeleteConcern/{id}`, `UpdateAuditor`, `UpdateRoadTestKm`,
  `UpsertRework`, `UpsertStageRework`, `UpdateStageDocuments`,
  `UploadStageFile`, `DeleteStageFile`, `SaveRfd`

Others: `MM_Vehicle_Details/GetVinDetails/{vin}`, `SaveVehicle`;
`MM_Shower_Test/ImportForVehicle`; `MM_Area_Image_Master/*` (§6);
`MM_Common/Get*List` (Shop/Model/AuditArea/Country/BuildPhase/Drive/Fuel/
Plant/VehicleArea/Severity); `MM_Auditor_Master/GetAuditorsByArea/{areaId}`;
`MM_*_Master/GetAll*`.

UI mirror: `Features/VehicleTracking/vehicle-tracking.service.ts` (+
`Features/Masters/master.service.ts` for masters).

---

## 10. SQL scripts (`API/SqlScripts/`, all idempotent)

Currently on disk (run on dev DB):
- `MM_Vehicle_Stage_Rework.sql` — stage-level rework table + index.
- `MM_Area_Image_Master.sql` — area images + part mappings + the 3 nullable
  click-point columns on MM_Vehicle_Defects.

Applied historically (files no longer in repo): base `MM_Vehicle_Track_Logs`,
`MM_Vehicle_Defects` (+ seeded Vehicle_Area/Severity), `MM_Vehicle_Defect_Rework`,
`Add_RoadTest_KM_Columns`. Paint Verification's master row (`Audit_Area_ID=12`)
was inserted manually by the user.

---

## 11. The user's working patterns (IMPORTANT — follow these)

1. **Plan first, then execute on approval.** For any non-trivial feature the
   user says "first tell me your plan / lets discuss" — give design (data
   model + API + UX), ask focused numbered questions, WAIT, then build.
   For small tweaks: just do it and say what to verify.
2. **Implementation order**: SQL → backend → frontend → wire to shell →
   typecheck + dotnet build → "how to verify" click-path list at the end.
3. **Be fast.** The user explicitly said "you are doing too much research —
   do it fast." Don't over-explore; this file exists to prevent that.
4. **One feature at a time.** No "while I'm here" refactors. Explicitly do
   NOT refactor stage components into a base class (user declined).
5. **Per-row auto-save** preferred (auditor dropdowns, KM, documents) —
   except where a modal with validation is the pattern (rework entries:
   Done + By + Remark all required before save enabled).
6. **Popups over links**: attachments always open in a thumbnail modal
   (paperclip icon + count), never bare download links.
7. **Renames are UI-label-only** (e.g., LSP Audit→Static Audit) — never
   rename folders/components/DB keys for a display change.
8. **The user hand-edits code between turns** — accept those changes
   silently, never revert (e.g., GetVinDetails stub, ShowerSpRowDto
   Detection_Time changed to string).
9. Non-native English, brief messages — read carefully, ask ONE clarifying
   question if genuinely ambiguous; don't guess and build big things.
10. Screenshots pointing at UI problems = urgent visual fixes.
11. The user runs SQL scripts manually and adds menu rows manually.
12. Polished UI matters: match existing Tailwind patterns (bg-white cards on
    gray-50, px-3 py-2 inputs, emerald/amber/red status colors, stage-style
    headers). When told "design not impressive — make like X", clone X's
    exact classes.

---

## 12. Deferred / future list

1. **RFD sticker printing** — deferred until printer hardware chosen. Full
   spec + decision rationale in §5 Physical RFD. ~1 day of work when ready.
2. **Image heat-map report** — consolidated defects-per-image-region report
   for management; click coords already stored.
3. **Paint Verification "major changes"** — user announced more changes coming.
4. **Rework done-list view** on standalone /rework (currently pending-only).
5. **ECOS→required?** currently info-only; VMS+DTC+Paint gate RFD.
6. VIN Tracking dashboards (PPT slides 15–21), 9PM daily summary email,
   audit-change notification emails (PPT slide 22) — SMTP plumbing exists
   (`MM_Common/SendTestMail`).
7. Admin CRUD for `MM_Vehicle_Area_Master` / `MM_Severity_Master`.
8. `UploadStageFile`/`DeleteStageFile` have no server-side RFD lock (no
   Vehicle_ID in payload) — UI gates suffice for now.
9. Tests — none exist, never requested.

---

## 13. Orientation reading list (only if this file isn't enough)

1. `UI/src/app/Features/VehicleTracking/vehicle-tracking-home/vehicle-tracking-home.component.ts` — shell, gating, locks.
2. `UI/src/app/Features/VehicleTracking/vehicle-tracking.models.ts` — STAGE map, ConcernRow, payloads.
3. `UI/src/app/Features/VehicleTracking/vehicle-tracking.service.ts` — every endpoint + footgun comment.
4. `UI/src/app/Features/VehicleTracking/shared/defect-entry-form/defect-entry-form.component.ts`
5. `API/Controllers/MM_Vehicle_TrackingController.cs` — folder whitelist, RfdLockedResponse.
6. `API/Data/MM_Vehicle_TrackingDataService.cs` — all tracking SQL, readiness logic.
7. Per-feature: `stages/<name>/`, `Features/Rework/`, `Features/Masters/areaimage/`,
   `API/Data/MM_ShowerTestDataService.cs`, `API/Data/MM_Area_ImageDataService.cs`.

## 14. TL;DR for a fresh session

1. Everything through Paint Verification image-based audit is BUILT and clean
   (tsc + dotnet build both pass).
2. The user gives one task at a time. Non-trivial → plan + questions first.
   Tweak → do it, typecheck, list verification clicks.
3. Respect the locks (§4), the api-request footgun (§2), the stage component
   contract (§4), and the user's patterns (§11).
4. Don't touch: auth/RBAC, GetVinDetails stub, pre-existing build warnings.
