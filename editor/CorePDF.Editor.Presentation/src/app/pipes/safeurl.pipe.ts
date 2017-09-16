
import {Pipe} from "@angular/core";
import {DomSanitizer, SafeHtml, SafeStyle, SafeScript, SafeUrl, SafeResourceUrl} from "@angular/platform-browser";

//https://forum.ionicframework.com/t/inserting-html-via-angular-2-use-of-domsanitizationservice-bypasssecuritytrusthtml/62562/2
@Pipe({
	name: 'safe'
})
export class SafePipe {

    constructor(protected _sanitizer: DomSanitizer) {
	}

	public transform(value: string, type: string): SafeHtml | SafeStyle | SafeScript | SafeUrl | SafeResourceUrl {
debugger;
		switch (type) {
			case 'html':
				return this._sanitizer.bypassSecurityTrustHtml(value);
			case 'style':
				return this._sanitizer.bypassSecurityTrustStyle(value);
			case 'script':
				return this._sanitizer.bypassSecurityTrustScript(value);
			case 'url':
				return this._sanitizer.bypassSecurityTrustUrl(value);
			case 'resourceUrl':
				return this._sanitizer.bypassSecurityTrustResourceUrl(value);
			default:
				throw new Error(`Unable to bypass security for invalid type: ${type}`);
		}
	}
}