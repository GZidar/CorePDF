import { Component, OnInit, EventEmitter, Input } from "@angular/core";
import { FormBuilder, FormControl, FormGroup, Validators } from "@angular/forms";
import { Router } from "@angular/router";
import { Response } from "@angular/http";

import { ApplicationInsightsService } from '@spa/app/services/application-insights.service';

@Component({
    moduleId: module.id,
    selector: "editor-page",
    templateUrl: "editor.component.html",
    styleUrls: ["editor.component.less"]
})
export class EditorComponent implements OnInit {
    submitted = false;

    constructor(
        private _appInsightsService: ApplicationInsightsService,
        private _router: Router,
        private _fb: FormBuilder,
    ) {
    }

    ngOnInit(): void {
        this._appInsightsService.trackPageView();
    }

    submitForm(event): void {
        event.preventDefault();
        if (this.submitted)
            return;
        
        this.submitted = true;
    }

}
