
/**
 * Shows or hides the controls in the UI
 *
 * @param {boolean} show
 */
function showHideControls(show) {
    var elements = document.querySelectorAll("div.controls");
    for (i = 0; i < elements.length; i++) {
        elements[i].style.display = (show
            ? 'block'
            : 'none');
    }
}

/**
 * Shows or hides the headers and footers of the charts
 *
 * @param {any} elCheckbox
 */
function toggleHeaderFooter(elCheckbox) {
    if (typeof elCheckbox === 'boolean') {
        showHideHeaderFooter(checked);
    } else {
        showHideHeaderFooter(elCheckbox.checked);
    }

}

/**
 * Executes the show-hide of the chart headers and footers
 *
 * @param {boolean} show
 */
function showHideHeaderFooter(show) {
    // Set the div elements
    var elements = document.querySelectorAll("div.tablegraph-header-wrapper, div.graph-data, div.tablegraph-footer-wrapper, hr");
    for (i = 0; i < elements.length; i++) {
        elements[i].style.display = (show
            ? 'block'
            : 'none');
    }

    // Set the checkbox
    document.getElementById('chk-display-headerfooter').checked = show;
}

/**
 * Shows or hides a single chart based on the id of the wrapper div
 *
 * @param {string} chartId
 * @param {boolean} show
 */
function showHideSingleChart(chartId, show) {
    if (chartId === 'all') {
        showHideAllCharts(true);
    } else {
        // Hide all the charts
        showHideAllCharts(false);

        // Change the visibility of the current chart
        document.getElementById(chartId).style.display = (show
            ? 'block'
            : 'none');
    }
}

/**
 * Shows or hides all of the charts on the page
 *
 * @param {boolean} show
 */
function showHideAllCharts(show) {
    var elements = document.querySelectorAll("div.chart-wrapper");
    for (i = 0; i < elements.length; i++) {
        elements[i].style.display = (show
            ? 'block'
            : 'none');
    }
}

/**
 * Loops through all the charts on the page and collects information that can be used by NodeJS when converting the charts to bitmaps
 *
 * @returns JSON string containing the chart information
 */
function getChartsInfo() {

    var chartInfo = {};
    var chartWrappers = document.querySelectorAll("div.chart-wrapper");

    for (i = 0; i < chartWrappers.length; i++) {
        var chartWrapper = chartWrappers[i];
        var chartId = chartWrapper.id;

        // Find the dimensions of the chart itself
        var chartContainer = chartWrapper.querySelector('div.chart-content');
        var width = chartContainer.offsetWidth;
        var height = chartContainer.offsetHeight;

        chartInfo[chartId] = {
            width: width,
            height: height
        }
    }

    return JSON.stringify(chartInfo);
}

function getChartTypes() {

    var chartWrappers = document.querySelectorAll("div.chart-wrapper");
    var uniquetypes = [];

    for (i = 0; i < chartWrappers.length; i++) {
        var type = chartWrappers[i].getAttribute('data-graphtype');
        uniquetypes.indexOf(type) === -1
            ? uniquetypes.push(type)
            : '';

    }

    // console.log(uniquetypes);
}

document.addEventListener('DOMContentLoaded', getChartTypes, false);
