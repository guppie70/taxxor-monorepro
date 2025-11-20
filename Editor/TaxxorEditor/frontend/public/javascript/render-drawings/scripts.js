var domIsLoaded = false;
var allDrawingsHaveBeenLoaded = false;
var totalDrawings = 0;
var drawingsLoaded = 0;
var loadingTime = 0;
var sampleTime = 0;
var maxTestForDrawingsLoaded = 100;
var logMessages = [];

let [timer, timingMonitor] = [0, () => timer = !timer ? Date.now() : `${Date.now() - timer}ms`]


/**
 * Shows or hides the controls in the UI
 */
function showHideControls(show) {
    var elements = document.querySelectorAll("div.controls");
    for (i = 0; i < elements.length; i++) {
        elements[i].style.display = (show ?
            'block' :
            'none');
    }
}

/**
 * Executes the show-hide of the chart headers and footers
 */
function showHideHeaderFooter(show) {
    // Set the div elements
    var elements = document.querySelectorAll("div.tablegraph-header-wrapper, div.graph-data, div.tablegraph-footer-wrapper, hr");
    for (i = 0; i < elements.length; i++) {
        elements[i].style.display = (show ?
            'block' :
            'none');
    }

    // Set the checkbox
    document.getElementById('chk-display-headerfooter').checked = show;
}



/**
 * Shows or hides all SVG drawings on the page
 */
function showHideAllSvgDrawings(show) {
    var useComments = true;

    if (!useComments) {
        var elements = document.querySelectorAll('div.illustration');
        for (i = 0; i < elements.length; i++) {
            elements[i].style.display = (show ?
                'block' :
                'none');
        }
    } else {
        // We are using HTML comments to show/hide elements just to be sure that the <style/> won't affect the styling of the SVG drawings
        $('div.illustration').contents().each(function (index, node) {
            if (show) {
                if (node.nodeType == 8) {
                    // node is a comment
                    if (typeof node.nodeValue !== 'undefined') {
                        $(node).replaceWith(node.nodeValue);
                    }
                }
            } else {
                var outerHtml = node.outerHTML;
                if (typeof outerHtml !== 'undefined') {
                    var comment = document.createComment(outerHtml);
                    $(node).replaceWith(comment);
                }
            }
        });
    }



}


/**
 * Shows or hides a single SVG drawing on the page
 */
function showHideSingleSvgDrawing(id, show) {
    var useComments = true;

    if (id === 'all') {
        showHideAllSvgDrawings(true);
    } else {
        // Hide all the SVG elements
        showHideAllSvgDrawings(false);

        if (!useComments) {
            // Change the visibility of the current chart
            document.getElementById(id).style.display = (show ?
                'block' :
                'none');
        } else {
            // Show this SVG by uncommenting it
            var elIllustrations = $(`div#${id}`);
            elIllustrations.contents().each(function (index, node) {
                if (show) {
                    if (node.nodeType == 8) {
                        // node is a comment
                        if (typeof node.nodeValue !== 'undefined') {
                            $(node).replaceWith(node.nodeValue);
                        }
                    }
                } else {
                    var outerHtml = $(node).get(0).outerHTML;
                    if (typeof outerHtml !== 'undefined') {
                        var comment = document.createComment(outerHtml);
                        $(node).replaceWith(comment);
                    }
                }
            });
            // $.wait(500).then(function(){return 'yoooo'});
        }
    }

}

/**
 * Hides an SVG drawing by commenting it out in the DOM
 */
function hideSingleSvgDrawing(id) {

    // Hide this SVG by uncommenting it
    var elIllustrations = $(`div#${id}`);
    elIllustrations.contents().each(function (index, node) {
        var outerHtml = $(node).get(0).outerHTML;
        if (typeof outerHtml !== 'undefined') {
            var comment = document.createComment(outerHtml);
            $(node).replaceWith(comment);
        }
    });
}

/**
 * Retrieves information about all the SVG ojects in the utility file
 */
function getSvgDrawingInfo() {

    var svgInfo = {};
    var svgWrappers = document.querySelectorAll("div.illustration");

    for (i = 0; i < svgWrappers.length; i++) {
        var svgWrapper = svgWrappers[i];
        var svgWrapperId = svgWrapper.id;

        // Grab information 

        var filenameConvert = svgWrapper.getAttribute('data-assetnameconvert');
        var filenameUse = svgWrapper.getAttribute('data-assetnameuse');

        // Use the original filename for conversion
        if (filenameConvert === null) {
            var svgContainer = svgWrapper.querySelector('object');
            var svgUri = svgContainer.getAttribute('data');
            var svgFileName = svgUri.replace(/^.*\/(.*).svg.*$/, '$1');
            filenameConvert = filenameUse = svgFileName + '.png';
        }


        var width = svgWrapper.offsetWidth;
        var height = svgWrapper.offsetHeight;

        svgInfo[svgWrapperId] = {
            filenameconvert: filenameConvert,
            filenameuse: filenameUse,
            width: width,
            height: height
        }
    }

    return JSON.stringify(svgInfo);
}

/**
 * Retrieves SVG data, but dumps the result in a debugging div or an alert box for debugging purposes
 */
function getSvgDrawingInfoDisplay() {
    var json = getSvgDrawingInfo();
    var elForDisplay = document.getElementById('tx-debugoutput');
    if (elForDisplay) {
        var jsonForDisplay = JSON.parse(json);
        elForDisplay.innerHTML = '<pre>' + JSON.stringify(jsonForDisplay, null, '  ') + '</pre>';
    } else {
        alert(json);
    }
}


/**
 * Utility function to delay the execution of the JS thread
 * @param {*} time 
 * @returns 
 */
function delay(time) {
    return new Promise(resolve => setTimeout(resolve, time));
}

/**
 * Utility function that logs in the console, but also stored the messages in an array so that the conversion service can grab it
 * @param {*} message 
 */
function log(message) {
    console.log(message);
    logMessages.push(message);
}

/**
 * Utility to clear the log array
 */
function clearLog() {
    logMessages = [];
}

/**
 * Retrieves the contents of the log
 * @returns 
 */
function getLog() {
    return logMessages.join('\n');
}


/**
 * Executed when an SVG drawing has been loaded in the utility HTML file
 * @param {*} uri 
 */
function drawingLoaded(uri) {
    drawingsLoaded++;
    log(`(${drawingsLoaded}/${totalDrawings}) -> drawing (${uri}) successfully fetched`)
    if (drawingsLoaded === totalDrawings) {
        log(`ALL ${totalDrawings} DRAWINGS LOADED`);
        allDrawingsHaveBeenLoaded = true;
    }
}

/**
 * Called when the page initiates
 */
var checkDrawingsLoaded = async () => {
    // async function checkDrawingsLoaded() {
    console.log('Start the SVG drawings monitor');
    var sampleFrequency = 100 //ms
    for (let i = 0; i < maxTestForDrawingsLoaded; i++) {
        sampleTime = (i * sampleFrequency);
        if (allDrawingsHaveBeenLoaded) {
            return ('SUCCESS: All ' + totalDrawings + ' drawings have been loaded');
        }
        await delay(sampleFrequency)
    }

    // After the max amount of checks and all drawings not loaded, we conclude that there must have been some sort of error
    log('ERROR: not all images were loaded');
    return ('ERROR: not drawings (total: ' + (totalDrawings) + ', failed: ' + (totalDrawings - drawingsLoaded) + ') have been loaded after ' + (maxTestForDrawingsLoaded * sampleFrequency) + 'ms');
}

/**
 * Called by the conversion service to test if all the SVG drawings have been loaded in the utility HTML file
 */
function drawingsLoadedStatistics() {
    if (allDrawingsHaveBeenLoaded) {
        return JSON.stringify({
            imagesLoaded: true,
            message: 'SUCCESS: All ' + totalDrawings + ' drawings have been loaded after approximately ' + ((loadingTime === 0) ? sampleTime + 'ms' : loadingTime),
            log: getLog(),
            processingtime: loadingTime
        })
    } else {
        return JSON.stringify({
            imagesLoaded: false,
            message: 'ERROR: not all drawings (total: ' + (totalDrawings) + ', failed: ' + (totalDrawings - drawingsLoaded) + ') have been loaded after ' + sampleTime + ' ms',
            log: getLog(),
            processingtime: sampleTime
        })
    }
}

/**
 * Test script to execute an async method from NodeJS code in headless chrome
 * @returns 
 */
async function testAsync() {
    console.log(`start - totalDrawings: ${totalDrawings}, drawingsLoaded ${drawingsLoaded}`);
    await delay(2000);
    console.log(`end - totalDrawings: ${totalDrawings}, drawingsLoaded ${drawingsLoaded}`);
    return 'done';
}

// Initiate timer
timingMonitor();

/**
 * Start the drawing rendering process once the complete DOM has been loaded
 */
$(async function () {
    domIsLoaded = true;


    var svgObjects = $("div.illustration object");
    totalDrawings = svgObjects.length;

    svgObjects.each(function (idx, elSvgObject) {
        // Attach loading event which fires as soon as the SVG content has been loaded into the object node
        elSvgObject.addEventListener('load', function (ev) {
            drawingLoaded(ev.srcElement.data)
        });
    });

    try {
        var drawingCheckResult = await checkDrawingsLoaded();
        loadingTime = timingMonitor();
        log('************************************');
        log(drawingCheckResult);
        log('total processing time: ' + loadingTime);
        log('************************************');
    } catch (err) {
        log('There was an error checkig the drawing load process');
        log(JSON.stringify(err));
    }

});