import { Component, OnInit, EventEmitter, Input } from "@angular/core";
import { JsonPipe } from "@angular/common";
import { FormBuilder, FormControl, FormGroup, Validators } from "@angular/forms";
import { Router } from "@angular/router";
import { Response } from "@angular/http";
import { DomSanitizer, SafeHtml, SafeStyle, SafeScript, SafeUrl, SafeResourceUrl } from "@angular/platform-browser";

import { EditorModel } from "@spa/app/models/editor.model";
import { PdfModel } from "@spa/app/models/pdf.model";
import { SafePipe } from "@spa/app/pipes/safeurl.pipe";

import { ApplicationInsightsService } from "@spa/app/services/application-insights.service";
import { EditorService } from "@spa/app/services/editor.service";

@Component({
    moduleId: module.id,
    selector: "editor-page",
    templateUrl: "editor.component.html",
    styleUrls: ["editor.component.less"]
})
export class EditorComponent implements OnInit {
    pageForm: FormGroup;
    submitted = false;
    temp: string;
    pdf: SafeResourceUrl;

    constructor(
        private _editorService: EditorService,
        private _appInsightsService: ApplicationInsightsService,
        private _router: Router,
        private _sanitizer: DomSanitizer,
        private _fb: FormBuilder
    ) {
        this.pdf = this._sanitizer.bypassSecurityTrustResourceUrl("http://infolab.stanford.edu/pub/papers/google.pdf");        
    }

    ngOnInit(): void {
        this._appInsightsService.trackPageView();

        this.pageForm = this._fb.group({
            DocumentJSON: ""
        });        
    }

    newDocument(event): void {
        event.preventDefault();
        //if (this.submitted)
        //    return;
        
        this.submitted = true;

        if (this.pageForm.valid) {
            this._editorService.getnewDocument().subscribe(
                response => {
                    this.submitted = false;
                    if (response != null && response != undefined) {
                        const model = response as EditorModel;
                        this.temp = model.documentData;
                        this.pageForm.controls["DocumentJSON"].setValue(model.documentData);
                    }
                },
                error => {
                    this.submitted = false;
                    console.error(error);
                });
        }
    }

    previewDocument(event): void {
        event.preventDefault();
        //if (this.submitted)
        //    return;
        
        this.submitted = true;

        if (this.pageForm.valid) {
            this._editorService.previewDocument(this.pageForm.controls["DocumentJSON"].value).subscribe(
                response => {
                    this.submitted = false;
                    if (response != null && response != undefined) {
                        const model = response as PdfModel;
                        this.pdf = this._sanitizer.bypassSecurityTrustResourceUrl("data:application/pdf;base64," + model.pdf);
                    }
                },
                error => {
                    this.submitted = false;
                    console.error(error);
                });
        }
    }    

}
