export interface Shop {
  Shop_ID: number;
  Shop_Name: string;
}

export class Model {
  Model_ID: number = 0;
  Shop_ID?: number;
  Model_Name: string = "";
  Is_LSP_Active:boolean = true;
  /** Current build phase of the model — auto-selected on Identify Vehicle. */
  Current_Build_Phase_ID?: number | null;
   Updated_Host?: string = localStorage.getItem('Hostname') ?? '';
  Updated_User_ID? = Number(localStorage.getItem('Employee_ID'));
}

export class UserList {
  Employee_ID: number = 0;
  Employee_Name: string = '';
  Designation_ID: number = 0;
  Designation_Name: string = '';
}

export class Shift {
  Shift_ID: number = 0;
  Shift_Name: string = '';
}

export class Audit_Area {
  Audit_Area_ID: number = 0;
  Audit_Area_Name: string = '';
}

export class AuditorList {
  Audit_Area_ID: number = 0;
  Audit_Area_Name?: string = '';
  Auditor_ID: number = 0;
  Auditor_Name: string = '';
  Plant_Code?: string = localStorage.getItem('Plant_Code') ?? '';
  Inserted_Host?: string = localStorage.getItem('Hostname') ?? '';
  Inserted_User_ID = Number(localStorage.getItem('Employee_ID'));
  Updated_Host?: string = localStorage.getItem('Hostname') ?? '';
  Updated_User_ID? = Number(localStorage.getItem('Employee_ID'));
}
