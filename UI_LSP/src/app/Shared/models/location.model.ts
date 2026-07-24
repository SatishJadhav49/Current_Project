export class LocationData {
    Location_ID?: number;
    Location_Name: string = '';
    SORTORDER?: number;
    Plant_Code?:string = localStorage.getItem('Plant_Code') ?? "";
    Inserted_Host?:string = localStorage.getItem('Hostname') ?? "";
    Inserted_User_ID = Number(localStorage.getItem('Employee_ID'));
    Updated_Host?:string = localStorage.getItem('Hostname') ?? "";
    Updated_User_ID? = Number(localStorage.getItem('Employee_ID'));
}
