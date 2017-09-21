import { Component } from '@angular/core';
import { NavController } from 'ionic-angular';
import { JsonPipe } from "@angular/common";
import { DomSanitizer, SafeResourceUrl } from "@angular/platform-browser";

import { PdfModel } from "../../models/pdf.model";

import { EditorService } from "../../services/editor.service";

@Component({
  selector: 'editor-page',
  templateUrl: 'editor.html'
})
export class EditorPage {

  submitted = false;
  pdf: SafeResourceUrl;
  showPDF: boolean = false;
  documentText: string = "";

  constructor(public navCtrl: NavController,
              private _editorService: EditorService,
              private _sanitizer: DomSanitizer) {

  }

  newDocument(event): void {
    event.preventDefault();

    this.submitted = true;
    this.documentText += "newdoc 1 ";
    this._editorService.getnewDocument().subscribe(
        response => {
            this.submitted = false;
            this.documentText += "newdoc 2 ";
            if (response != null && response != undefined) {
                this.documentText += "newdoc 3 ";
                const json = JSON.parse(response.documentData);
                const pipe = new JsonPipe();
                this.documentText = pipe.transform(json);
            }
        },
        error => {
            this.documentText += "newdoc 4 ";
            this.submitted = false;
            this.documentText += error;
        });
    
  }

  previewDocument(event): void {
    event.preventDefault();

    this.submitted = true;

    this._editorService.previewDocument(this.documentText).subscribe(
        response => {
            this.submitted = false;
            if (response != null && response != undefined) {
                const model = response as PdfModel;
                this.pdf = this._sanitizer.bypassSecurityTrustResourceUrl("data:application/pdf;base64," + model.pdf);
                this.showPDF = true;
            }
        },
        error => {
            this.submitted = false;
            this.documentText = error;
        });
  }    

  closePreview(): void {
    this.showPDF = false;
  }
}
