export class Cards_Data {
  New: number = 0;
  Open: number = 0;
  Closed: number = 0;
  Total: number = 0;
}

export class SourceWiseDestribution {
  Source_ID: number = 0;
  Source_Name: string = '';
  Open: number = 0;
  Closed: number = 0;
}

export class AttributionWiseDestribution {
  Attribution_ID: number = 0;
  Attribution_Name: string = '';
  Open: number = 0;
  Closed: number = 0;
}

export class ProcessStagesWiseDistribution {
  RP_ID: number = 0;
  Process_Name: string = '';
  Open: number = 0;
}

export class SeverityWiseDistribution {
  Severity_ID: number = 0;
  Severity_Name: string = '';
  Open: number = 0;
  Closed: number = 0;
}