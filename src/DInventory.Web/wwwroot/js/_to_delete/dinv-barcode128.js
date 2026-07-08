/*!
 * DInvBarcode - minimal, dependency-free CODE128 (subset B) barcode renderer.
 * Draws directly into an inline <svg> element. No external requests, no CDN.
 *
 * Usage:
 *   DinvBarcode.render('mySvgId', 'DIN000000123', { width: 2, height: 50, displayValue: true, fontSize: 14, margin: 4 });
 *
 * Throws an Error for characters outside the encodable range (ASCII 32-126) so callers
 * can fall back to a plain-text label, same as they previously did for a JsBarcode failure.
 */
(function (global) {
    'use strict';

    // Width-pattern table for CODE128 values 0-105 (6 bar/space widths each) plus the
    // STOP pattern at index 106 (7 widths). This table is shared by all three code sets;
    // only the character-to-value mapping differs. We only implement Code Set B here,
    // which covers the full printable ASCII range (32-126) - plenty for SKU-style codes,
    // manually typed codes, and anything a handheld scanner would type into the field.
    var PATTERNS = [
        '212222', '222122', '222221', '121223', '121322', '131222', '122213', '122312', '132212', '221213',
        '221312', '231212', '112232', '122132', '122231', '113222', '123122', '123221', '223211', '221132',
        '221231', '213212', '223112', '312131', '311222', '321122', '321221', '312212', '322112', '322211',
        '212123', '212321', '232121', '111323', '131123', '131321', '112313', '132113', '132311', '211313',
        '231113', '231311', '112133', '112331', '132131', '113123', '113321', '133121', '313121', '211331',
        '231131', '213113', '213311', '213131', '311123', '311321', '331121', '312113', '312311', '332111',
        '314111', '221411', '431111', '111224', '111422', '121124', '121421', '141122', '141221', '112214',
        '112412', '122114', '122411', '142112', '142211', '241211', '221114', '413111', '241112', '134111',
        '111242', '121142', '121241', '114212', '124112', '124211', '411212', '421112', '421211', '212141',
        '214121', '412121', '111143', '111341', '131141', '114113', '114311', '411113', '411311', '113141',
        '114131', '311141', '411131', '211412', '211214', '211232',
        '2331112' // STOP (index 106)
    ];

    var START_B = 104;
    var STOP = 106;

    function encode(text) {
        if (typeof text !== 'string' || text.length === 0) {
            throw new Error('Barcode value must be a non-empty string.');
        }

        var values = [START_B];
        for (var i = 0; i < text.length; i++) {
            var code = text.charCodeAt(i);
            if (code < 32 || code > 126) {
                throw new Error('Character "' + text[i] + '" cannot be encoded in CODE128.');
            }
            values.push(code - 32);
        }

        var checksum = values[0];
        for (var j = 1; j < values.length; j++) {
            checksum += values[j] * j;
        }
        values.push(checksum % 103);
        values.push(STOP);

        return values.map(function (v) { return PATTERNS[v]; });
    }

    function render(target, text, options) {
        var opts = options || {};
        var moduleWidth = opts.width || 2;
        var barHeight = opts.height || 50;
        var displayValue = opts.displayValue !== false;
        var fontSize = opts.fontSize || 14;
        var margin = opts.margin != null ? opts.margin : 10;

        var svg = typeof target === 'string' ? document.getElementById(target) : target;
        if (!svg) {
            throw new Error('Barcode target element not found.');
        }

        var patterns = encode(text); // throws on invalid input - let caller catch it

        var totalUnits = 0;
        patterns.forEach(function (p) {
            for (var k = 0; k < p.length; k++) totalUnits += parseInt(p[k], 10);
        });

        var barsWidth = totalUnits * moduleWidth;
        var textHeight = displayValue ? fontSize + 6 : 0;
        var svgWidth = barsWidth + margin * 2;
        var svgHeight = barHeight + textHeight + margin;

        while (svg.firstChild) svg.removeChild(svg.firstChild);
        svg.setAttribute('width', svgWidth);
        svg.setAttribute('height', svgHeight);
        svg.setAttribute('viewBox', '0 0 ' + svgWidth + ' ' + svgHeight);

        var x = margin;
        var isBar = true; // patterns always start with a bar (black)
        patterns.forEach(function (pattern) {
            for (var k = 0; k < pattern.length; k++) {
                var widthUnits = parseInt(pattern[k], 10);
                var widthPx = widthUnits * moduleWidth;
                if (isBar) {
                    var rect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
                    rect.setAttribute('x', x);
                    rect.setAttribute('y', 0);
                    rect.setAttribute('width', widthPx);
                    rect.setAttribute('height', barHeight);
                    rect.setAttribute('fill', '#000');
                    svg.appendChild(rect);
                }
                x += widthPx;
                isBar = !isBar;
            }
        });

        if (displayValue) {
            var textEl = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            textEl.setAttribute('x', svgWidth / 2);
            textEl.setAttribute('y', barHeight + fontSize);
            textEl.setAttribute('text-anchor', 'middle');
            textEl.setAttribute('font-family', 'monospace');
            textEl.setAttribute('font-size', fontSize);
            textEl.textContent = text;
            svg.appendChild(textEl);
        }
    }

    global.DinvBarcode = { render: render };
})(window);
