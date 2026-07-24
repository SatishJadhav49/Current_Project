import { Routes } from "@angular/router";

export const DocumentsRoutes: Routes =[

    {
        path:'documents',
        loadComponent:() => import('./documentsmanagement/documentsmanagement.component').then(m=>m.DocumentsmanagementComponent)
    },

]