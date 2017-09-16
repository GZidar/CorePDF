import { Component, OnInit } from "@angular/core";

import { BuildInformationModel } from "@spa/app/models/build-information.model";
import { BuildInformationService } from "@spa/app/services/build-information.service";

@Component({
    moduleId: module.id,
    selector: "build-information-component",
    templateUrl: "build-information.component.html",
    styleUrls: ["build-information.component.less"]
})

export class BuildInformationComponent implements OnInit {
    model: BuildInformationModel;

    constructor(private buildInformationService: BuildInformationService) {
        this.model = {
            applicationVersion: ""
        } as BuildInformationModel;
    }

    ngOnInit() {
        this.buildInformationService.getBuildInformation().subscribe(response => {
            if (response != null && response != undefined) {
                this.model = response as BuildInformationModel;
            }
        },
        console.error);
    }
};
