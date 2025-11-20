const gulp = require('gulp');
const fs = require('fs');
const path = require('path');

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
const runningInContainer = (getEnvironmentVariable('DOTNET_RUNNING_IN_CONTAINER') === true || getEnvironmentVariable('DOTNET_RUNNING_IN_CONTAINER') === 'true') ?? false;


/**
 * Copies the root and SSL certificate for this application into the application root directory
 * Required because docker does not allow these to be dynamically added from a central directory located outside the build context
 */
const copyCertificates = (done) => {
    const taxxorRootFolderPathOs = path.dirname(path.dirname(applicationRootPathOs));

    if (currentEnvironment === 'default') {
        try {
            // Taxxor root certificate
            fs.copyFileSync(`${taxxorRootFolderPathOs}/_utils/Certificates/Taxxor_root.crt`, `${applicationRootPathOs}/DocumentStore/Taxxor_root.crt`)

            // SSL certificate
            const sslCertificateFileName = 'documentstore.pfx';
            fs.copyFileSync(`${taxxorRootFolderPathOs}/_utils/Certificates/${sslCertificateFileName}`, `${applicationRootPathOs}/DocumentStore/${sslCertificateFileName}`)
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
 * Combines the methods above in one series that can be executed by gulp
 */
const sequence = gulp.series(copyCertificates);

const basesequence = gulp.series(copyCertificates);

/**
 * Default gulp task
 */
exports.default = sequence;

exports.base = basesequence;