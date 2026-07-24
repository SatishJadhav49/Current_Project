import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface AlertData {
  vinNo: string;
  model: string;
  variant: string;
  severity: 'V1+' | 'V1';
  concern: string;
}

@Injectable({
  providedIn: 'root',
})
export class TeamsWebhookService {
  private readonly webhookUrl =
    'https://default8c4858b5f020483ab7ef71ded6e817.67.environment.api.powerplatform.com:443/powerautomate/automations/direct/workflows/613568336c3e413ea0053c72eec5e7aa/triggers/manual/paths/invoke?api-version=1&sp=%2Ftriggers%2Fmanual%2Frun&sv=1.0&sig=wodvwHaFQuQykhdbegmJiwnHtvbZ0M3qKlISAtC4_lA';

  constructor(private http: HttpClient) {}

  sendAlert(data: AlertData): Observable<any> {
    const headers = new HttpHeaders({
      'Content-Type': 'application/json',
    });

    // Determine color based on severity for professional look
    const colorMap = {
      V1: 'Attention',
      'V1+': 'Attention',
    };

    const now = new Date();

    const today =
      now
        .toLocaleDateString('en-GB', {
          day: '2-digit',
          month: 'short',
          year: 'numeric',
        })
        .replace(/ /g, '-') +
      ' ' +
      now.toLocaleTimeString('en-US', {
        hour: '2-digit',
        minute: '2-digit',
        hour12: true,
      });

    // Professional Adaptive Card Payload
    const payload = {
      type: 'message',
      attachments: [
        {
          contentType: 'application/vnd.microsoft.card.adaptive',
          content: {
            type: 'AdaptiveCard',
            $schema: 'http://adaptivecards.io',
            version: '1.4',
            body: [
              // {
              //   type: 'TextBlock',
              //   text: 'LSP Audit Notification',
              //   weight: 'Bolder',
              //   size: 'Large',
              //   style: 'heading',
              // },
              {
                type: 'FactSet',
                facts: [
                  { title: 'Date:', value: today },
                  { title: 'VIN No:', value: data.vinNo },
                  { title: 'Model:', value: data.model },
                  { title: 'Variant:', value: data.variant },
                ],
              },
              {
                type: 'Container',
                style: colorMap[data.severity] || 'Default',
                bleed: true,
                items: [
                  {
                    type: 'TextBlock',
                    text: `**Severity:** ${data.severity}`,
                    wrap: true,
                  },
                  {
                    type: 'TextBlock',
                    text: `**Concern:** ${data.concern}`,
                    wrap: true,
                  },
                ],
              },
            ],
          },
        },
      ],
    };

    return this.http.post(this.webhookUrl, payload, { headers });
  }
}
