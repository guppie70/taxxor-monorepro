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
 * Shows or hides the headers and footers of the charts
 */
function toggleHeaderFooter(elCheckbox) {
    if (typeof elCheckbox === 'boolean') {
        showHideHeaderFooter(checked);
    } else {
        showHideHeaderFooter(elCheckbox.checked);
    }

}

/**
 * Shows or hides the data tables for the graphs
 */
function toggleDataTables(elCheckbox) {
    if (typeof elCheckbox === 'boolean') {
        showHideDataTables(checked);
    } else {
        showHideDataTables(elCheckbox.checked);
    }
}

/**
 * Executes the show-hide of the chart headers and footers
 */
function showHideHeaderFooter(show) {
    // Set the div elements
    var elements = document.querySelectorAll("#graph-wrapper div.tablegraph-header-wrapper, #graph-wrapper div.graph-data, #graph-wrapper div.tablegraph-footer-wrapper, #graph-wrapper hr");
    for (i = 0; i < elements.length; i++) {
        elements[i].style.display = (show ?
            'block' :
            'none');
    }

    // Set the checkbox
    document.getElementById('chk-display-headerfooter').checked = show;
}

/**
 * Toggles the visibility of the data tables that feed the graphs
 */
function showHideDataTables(show) {
    // Set the div elements
    var elements = document.querySelectorAll("div.chart-wrapper table, div.c-chart table");
    for (i = 0; i < elements.length; i++) {
        elements[i].style.display = (show ?
            'block' :
            'none');
    }

    // Set the checkbox
    document.getElementById('chk-display-datatables').checked = show;
}

/**
 * Shows or hides a single chart based on the id of the wrapper div
 */
function showHideSingleChart(chartId, show) {
    if (chartId === 'all') {
        showHideAllCharts(true);
    } else {
        // Hide all the charts
        showHideAllCharts(false);

        // Change the visibility of the current chart
        document.getElementById(chartId).style.display = (show ?
            'block' :
            'none');
    }
}

/**
 * Shows or hides all of the charts on the page
 */
function showHideAllCharts(show) {
    var elements = document.querySelectorAll("div.chart-wrapper");
    for (i = 0; i < elements.length; i++) {
        elements[i].style.display = (show ?
            'block' :
            'none');
    }
}

/**
 * Loops through all the charts on the page and collects information that can be used by NodeJS when converting the charts to bitmaps
 */
function getChartsInfo() {

    var chartInfo = {};
    var chartWrappers = document.querySelectorAll("div.chart-wrapper, div.c-chart");

    for (i = 0; i < chartWrappers.length; i++) {
        var chartWrapper = chartWrappers[i];
        var chartId = chartWrapper.id;

        // Grab information from the data tables
        var dataTable = chartWrapper.querySelector('table');
        var filenameConvert = dataTable.getAttribute('data-assetnameconvert');
        var filenameUse = dataTable.getAttribute('data-assetnameuse');

        // Find the dimensions of the chart itself
        var chartContainer = chartWrapper.querySelector('div.highcharts-container, div.tx-renderedchart');
        // console.log(typeof chartContainer);
        // console.dir(chartContainer);
        if (chartContainer !== null && typeof chartContainer === 'object') {
            var width = chartContainer.offsetWidth;
            var height = chartContainer.offsetHeight;

            chartInfo[chartId] = {
                filenameconvert: filenameConvert,
                filenameuse: filenameUse,
                width: width,
                height: height
            }
        } else {
            chartInfo[chartId] = {
                filenameconvert: filenameConvert,
                filenameuse: filenameUse
            }
        }

    }

    return JSON.stringify(chartInfo);
}

/**
 * Renders a visible version of the charts information JSON content which is used in the conversion service to generate images from charts
 */
function getChartsInfoDisplay() {
    var json = getChartsInfo();
    var elForDisplay = document.getElementById('tx-debugoutput');
    if (elForDisplay) {
        elForDisplay.innerHTML = json;
    } else {
        alert(json);
    }
}

/**
Retrieves the SVG that highcharts has rendered for a graph
*/
function getRenderedChartSvg(wrapperId) {
    var elWrapper = $('div#' + wrapperId);
    if (elWrapper.length && elWrapper.length > 0) {
        var elChartContent = elWrapper.find('div.chart-content svg');
        if (elChartContent.length && elChartContent.length > 0) {
            return elChartContent[0].outerHTML;
        } else {
            console.log('Unable to find rendered graph svg for ID ' + wrapperId);
        }
    }
    return '';
}

/**
 * Retrieves all the SVG content of the graphs that have been rendered
 */
function retrieveAllRenderdedCharts() {
    var svgContent = {};
    var graphWrappers = $('div#graph-wrapper div[data-graphtype], div.c-chart');
    for (var index = 0; index < graphWrappers.length; index++) {
        var elWrapper = graphWrappers[index];
        var id = elWrapper.id;
        var elChartContent = $(elWrapper).find('div.chart-content svg, div.tx-renderedchart svg');
        if (elChartContent.length && elChartContent.length > 0) {
            svgContent[id] = elChartContent[0].outerHTML;
        } else {
            console.log('Unable to find rendered graph svg for ID ' + id);
        }
    }
    return JSON.stringify(svgContent);
}

/**
 * Retrieves a visible version of the JSON that is used in the conversion service for rendering the graphs
 */
function retrieveAllRenderdedChartsDisplay() {
    var json = retrieveAllRenderdedCharts();
    var elForDisplay = document.getElementById('tx-debugoutput');
    if (elForDisplay) {
        elForDisplay.innerHTML = htmlEscape(json);
    } else {
        alert(json);
    }
}

/**
 * HTML escapes a string
 */
function htmlEscape(str) {
    return str
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');
}

function getChartTypes() {

    var chartWrappers = document.querySelectorAll("div.chart-wrapper, div.c-chart");
    var uniquetypes = [];

    for (i = 0; i < chartWrappers.length; i++) {
        var chartWrapper = chartWrappers[i];
        var type = chartWrapper.getAttribute('data-graphtype');

        if (type === null) {
            type = chartWrapper.getAttribute('data-charttype');

            if (type === null) {
                // If data-graphtype or data-charttype attribute doesn't exist, inspect the classes
                var classes = chartWrapper.className.split(' ');
                var chartTypeClass = classes.find(function (cls) {
                    return cls.startsWith('c-chart--');
                });

                if (chartTypeClass !== undefined) {
                    type = chartTypeClass.replace('c-chart--', '');
                } else {
                    type = 'unknown';
                }
            }
        }

        uniquetypes.indexOf(type) === -1 ?
            uniquetypes.push(type) :
            '';
    }

    // console.log(uniquetypes);
}

document.addEventListener('DOMContentLoaded', getChartTypes, false);