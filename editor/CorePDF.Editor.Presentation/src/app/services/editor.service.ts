import 'rxjs/add/operator/filter';
import 'rxjs/add/operator/map';

import { Injectable } from "@angular/core";
import { Http, Headers, RequestOptions } from "@angular/http";

import { EditorModel } from "@spa/app/models/editor.model";

@Injectable()
export class EditorService {
    private apiBaseUrl: string = (<any>window).serviceHost;

    constructor(private http: Http) { }

    getnewDocument(): any {
        const headers: Headers = new Headers({ "Content-Type": "application/json" });
        const options: RequestOptions = new RequestOptions({ headers: headers });

        return this.http.get(this.apiBaseUrl + "/api/editor", options)
            .map(response => <EditorModel>response.json());
    }

    previewDocument(documentJson: string): any {
        const headers: Headers = new Headers({ "Content-Type": "application/json" });
        const options: RequestOptions = new RequestOptions({ headers: headers });
        const body = "";

        return this.http.post(this.apiBaseUrl + "/api/editor", "'" + documentJson +"'", options)
            .map(response => <EditorModel>response.json());
    }    
}