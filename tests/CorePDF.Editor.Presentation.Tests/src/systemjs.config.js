(function (global) {
    // map tells the System loader where to look for things
    var map = {
        "@spa": "spa",
        "@angular": "node_modules/@angular",
        "rxjs": "node_modules/rxjs"
    };

    // packages tells the System loader how to load when no filename and/or no extension
    var packages = {
        "@spa/app": {
            "main": "main.js",
            "defaultExtension": "js"
        },
        "rxjs": {
            "defaultExtension": "js"
        },
        "testing": {
            "main": "index.js",
            "defaultExtension": "js"
        },
        "specs": {
            "defaultExtension": "js"
        }
    };

    var ngPackageNames = [
        "animations",
        "common",
        "compiler",
        "core",
        "forms",
        "http",
        "platform-browser",
        "platform-browser-dynamic",
        "platform-browser/animations",
        "router"
    ];

    // Add package and map entries for angular packages
    ngPackageNames.forEach(function (pkgName) {
        packages["@angular/" + pkgName] = {
            "main": pkgName + ".umd.js",
            "defaultExtension": "js"
        };

        map["@angular/" + pkgName] = "node_modules/@angular/" + pkgName + "/bundles";
        map["@angular/" + pkgName + "/testing"] = "node_modules/@angular/" + pkgName + "/bundles/" + pkgName + "-testing.umd.js";
    });

    Error.stackTraceLimit = 0; // "No stacktrace"" is usually best for app testing.

    // Uncomment to get full stacktrace output. Sometimes helpful, usually not.
    // Error.stackTraceLimit = Infinity; //

    jasmine.DEFAULT_TIMEOUT_INTERVAL = 3000;

    var baseURL = document.baseURI;
    baseURL = baseURL + baseURL[baseURL.length - 1] ? '' : '/';

    var config = {
        baseURL: baseURL,
        map: map,
        packages: packages,
        
    };

    System.config(config);

    // Now kick of the test bootstrapping
    initTestBed().then(initTesting);

    const onloadWithJasmine = window.onload;
    window.onload = () => { };

    function initTestBed() {
        return Promise.all([
            System.import('@angular/core/testing'),
            System.import('@angular/platform-browser-dynamic/testing')
        ])

            .then(function (providers) {
                var coreTesting = providers[0];
                var browserTesting = providers[1];

                coreTesting.TestBed.initTestEnvironment(
                    browserTesting.BrowserDynamicTestingModule,
                    browserTesting.platformBrowserDynamicTesting());
            })
    }

    // Import all spec files defined in the html (__spec_files__)
    // and start Jasmine testrunner
    function initTesting() {
        console.log('loading spec files: ' + __spec_files__.join(', '));
        return Promise.all(
            __spec_files__.map(function (spec) {
                return System.import(spec);
            })
        )
        //  After all imports load,  re-execute `window.onload` which
        //  triggers the Jasmine test-runner start or explain what went wrong
        .then(success, console.error.bind(console));

        function success() {
            console.log('Spec files loaded; starting Jasmine testrunner');

            onloadWithJasmine();
        }
    }


})(this);