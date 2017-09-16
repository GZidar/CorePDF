import { Routes, RouterModule, Data } from "@angular/router";

import { AppComponent } from "@spa/app/app.component";

//Pages
import { EditorComponent } from "@spa/app/components/editor.component";

//interfaces

export const appRoutes: Routes = [
    {
        path: '',
        redirectTo: '/editor',
        pathMatch: 'full'
    },
    {
        path: 'editor',
        component: EditorComponent
    }

];

export const routing = RouterModule.forRoot(appRoutes);
