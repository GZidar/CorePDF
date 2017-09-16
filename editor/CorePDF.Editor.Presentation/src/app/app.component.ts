import { Component, ViewEncapsulation } from "@angular/core";

@Component({
    moduleId: module.id,
    selector: 'app-root',
    templateUrl: 'app.component.html',
    styleUrls: ['app.component.less'],
    encapsulation: ViewEncapsulation.None,
})

export class AppComponent {
    title = 'CorePDF Live Document Editor';

    constructor() {
       // When the user scrolls down 20px from the top of the document, show the button
        window.onscroll = this.scrollFunction;
    }

    scrollFunction(): void {
        if (document.body.scrollTop > 20 || document.documentElement.scrollTop > 20) {
            document.getElementById("top").style.display = "block";
        } else {
            document.getElementById("top").style.display = "none";
        }
    }

    scrollToTop() {
        window.scrollTo(0, 0);
    }
}
