import { Component, OnInit, EventEmitter, Input } from "@angular/core";
import { FormBuilder, FormControl, FormGroup, Validators } from "@angular/forms";
import { Router } from "@angular/router";
import { Response } from "@angular/http";

//import { LoginService } from "@spa/app/service/login.service";

@Component({
    moduleId: module.id,
    selector: "editor-page",
    templateUrl: "editor.component.html",
    styleUrls: ["editor.component.less"]
})
export class EditorComponent implements OnInit {
    submitted = false;

    constructor(
        //public loginService: LoginService,

        protected _router: Router,
        private fb: FormBuilder,
    ) {

    }

    ngOnInit(): void {
    }

    submitForm(event): void {
        event.preventDefault();
        if (this.submitted)
            return;
        
        this.submitted = true;
    }

}
