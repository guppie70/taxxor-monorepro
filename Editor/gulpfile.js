/**
 * NodeJS Gulp tasks that prepares the application to start
 */
const gulp = require('gulp');
const fs = require('fs');

// Global variables
const applicationRootPathOs = __dirname.replace(/\\/g, '/').replace(/^(.*)\/$/, '$1');

const getEnvironmentVariable = (envVarName) => {
    try {
        return process.env[envVarName];
    } catch (error) {
        console.error(`Error: The environment variable '${envVarName}' does not exist.`);
        return null;
    }
};

const currentEnvironment = getEnvironmentVariable('TAXXOR_DEV_ENVIRONMENT') ?? 'default';


/**
 * Copies the root and SSL certificate for this application into the application root directory
 * Required because docker does not allow these to be dynamically added from a central directory located outside the build context
 */
const copyCertificates = (done) => {
    if (currentEnvironment === 'default') {
        try {
            // Taxxor root certificate
            fs.copyFileSync(`../../_utils/Certificates/Taxxor_root.crt`, `${applicationRootPathOs}/TaxxorEditor/Taxxor_root.crt`)

            // SSL certificate
            const sslCertificateFileName = 'editor.pfx';
            fs.copyFileSync(`../../_utils/Certificates/${sslCertificateFileName}`, `${applicationRootPathOs}/TaxxorEditor/${sslCertificateFileName}`)
        } catch (e) {
            console.warn(`Could not copy certificate files, probably because we are running in a docker context`);
            console.warn(e);
        }
    } else {
        console.log('Skipping certificate copy');
    }

    done();
}



/**
 * Initialize tasks for compiling front-end assets and runs dotnet in watch mode as well
 */
gulp.task('dotnet',
    gulp.series(
        copyCertificates
    )
);