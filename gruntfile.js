/// <binding ProjectOpened='devBuild' />
var presentationProject = "editor/CorePDF.Editor.Presentation";
var src = presentationProject + "/src";
var spec = "../tests/CorePDF.Editor.Presentation.Tests/src";

var webProject = "editor/CorePDF.Editor.API";
var wwwroot = webProject + "/wwwroot";

module.exports = function (grunt) {

    grunt.initConfig({
        // Copy all JS files from external libraries and required NPM packages to src/js
        copy: {
            fonts: {
                files: [
                    {
                        expand: true,
                        dest: src + "/app/fonts",
                        nonull: true,
                        flatten: true,
                        src: [
                            "node_modules/font-awesome/fonts/*"
                        ]
                    },
                    {
                        expand: true,
                        dest: src + "/app/css",
                        nonull: true,
                        flatten: true,
                        src: [
                            "node_modules/font-awesome/css/font-awesome.min.css"
                        ]
                    }
                ]
            },
            deploy: {
                files: [
                    {
                        dest: wwwroot,
                        nonull: true,
                        flatten: true,
                        expand: true,
                        src: [
                            "node_modules/font-awesome/css/font-awesome.min.css"
                        ]
                    },
                    {
                        dest: wwwroot + "/fonts",
                        nonull: true,
                        flatten: true,
                        expand: true,
                        src: [
                            presentationProject + "/src/app/fonts/*.*"
                        ]
                    },
                    {
                        dest: wwwroot + "/assets",
                        nonull: true,
                        flatten: true,
                        expand: true,
                        src: [
                            presentationProject + "/src/assets/*.*"
                        ]
                    },
                    {
                        dest: wwwroot + "/",
                        nonull: true,
                        flatten: true,
                        expand: true,
                        rename: function (dest, src) {
                            return dest + "inline.bundle.js";
                        },
                        src: [
                            presentationProject + "/dist/inline.*.bundle.js"
                        ]
                    },
                    {
                        dest: wwwroot + "/",
                        nonull: true,
                        flatten: true,
                        expand: true,
                        rename: function (dest, src) {
                            return dest + "main.bundle.js";
                        },
                        src: [
                            presentationProject + "/dist/main.*.bundle.js"
                        ]
                    },
                    {
                        dest: wwwroot + "/",
                        nonull: true,
                        flatten: true,
                        expand: true,
                        rename: function (dest, src) {
                            return dest + "polyfills.bundle.js";
                        },
                        src: [
                            presentationProject + "/dist/polyfills.*.bundle.js"
                        ]
                    },
                    {
                        dest: wwwroot + "/",
                        nonull: true,
                        flatten: true,
                        expand: true,
                        rename: function (dest, src) {
                            return dest + "vendor.bundle.js";
                        },
                        src: [
                            presentationProject + "/dist/vendor.*.bundle.js"
                        ]
                    }
                ]
            }
        },
        shell: {
            options: {
                stdout: true,
                stderr: true,
                execOptions: {
                    maxBuffer: Infinity
                }
            },
            compileAndRunDevJIT: {
                command: [
                    '"node_modules/.bin/ng" serve'
                ].join(" && ")
            },
            compileAOT: {
                options: {
                    stdout: true
                },
                command: [
                    '"node_modules/.bin/ng" build --prod --aot true'
                ].join(' && ')
            },
            jasmineUI: {
                command: [
                    'cd Tests/TSP.Presentation.Tests',
                    'tsc',
                    '"../../node_modules/.bin/lite-server" -c=bs-config.json'
                ].join(' && ')
            },
            karma: {
                command: [
                    'cd Tests/TSP.Presentation.Tests',
                    'tsc',
                    'cd ../..',
                    '"node_modules/.bin/karma" start --single-run'
                ].join(' && ')
            }
        },

        clean: {
            build: {
                src: [
                    src + '/app/css/*.*',
                    src + '/app/fonts/*.*',
                    src + '/app/**/*.js',
                    src + '/app/**/*.css',
                    src + '/app/**/*.js.map',
                    src + '/app/**/*.ngstyle.ts',
                    src + '/app/**/*.ngfactory.ts',
                    src + '/app/**/*.ngsummary.json',
                    src + '/app/**/*.metadata.json',
                    src + "/main-jit.js*",
                    src + "/main-aot.js*",
                    spec + '/specs/**/*.js',
                    spec + '/specs/**/*.js.map',
                    wwwroot + "/*"
                ]
            }
        }
    });

    grunt.loadNpmTasks("grunt-contrib-clean");
    grunt.loadNpmTasks("grunt-contrib-copy");
    grunt.loadNpmTasks("grunt-shell");

    grunt.registerTask("devBuild", [
        "clean:build",
        "copy:fonts",
        "shell:compileAndRunDevJIT"
    ]);

    grunt.registerTask("aotBuild", [
        "clean:build",
        "copy:fonts",
        "shell:compileAOT"
    ]);

    grunt.registerTask("deploy", [
        "aotBuild",
        "copy:deploy"
    ]);

    grunt.registerTask("angularTests", [
        "shell:jasmineUI"
    ]);

};
