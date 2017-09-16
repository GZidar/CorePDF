import 'rxjs/add/operator/filter';
import 'rxjs/add/operator/map';

import { Injectable } from "@angular/core";
import { Http, Headers, RequestOptions } from "@angular/http";

import { BuildInformationModel } from "@spa/app/models/build-information.model";

@Injectable()
export class BuildInformationService {
    apiBaseUrl: string = (<any>window).serviceHost;

    constructor(private http: Http) { }

    getBuildInformation(): any {
        const headers: Headers = new Headers({ "Content-Type": "application/json" });
        const options: RequestOptions = new RequestOptions({ headers: headers });
        // Add a comment
        debugger;

        return this.http.get(this.apiBaseUrl + "/api/information", options)
            .map(response => <BuildInformationModel>response.json());
    }
}