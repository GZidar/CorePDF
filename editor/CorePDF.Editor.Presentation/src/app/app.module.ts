import { NgModule } from "@angular/core";
import { BrowserModule } from "@angular/platform-browser";
import { FormsModule, ReactiveFormsModule } from "@angular/forms";
import { HttpModule } from "@angular/http";
import { CommonModule } from "@angular/common";

//app
import { AppComponent } from "@spa/app/app.component";
import { routing } from "@spa/app/app.routes";

//Services
import { BuildInformationService } from "@spa/app/services/build-information.service";

//Components
import { EditorComponent } from "@spa/app/components/editor.component";
import { BuildInformationComponent } from "@spa/app/components/build-information.component";

@NgModule({
    declarations: [
        AppComponent,
        EditorComponent,
        BuildInformationComponent
    ],
    imports: [
        BrowserModule,
        routing,
        FormsModule,
        ReactiveFormsModule,
        HttpModule,
        CommonModule
    ],
    bootstrap: [
        AppComponent
    ],
    providers: [
        BuildInformationService
    ]
})
export class AppModule { }
