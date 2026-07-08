/*!
 * DInvBarcode - minimal, dependency-free EAN-13 barcode renderer.
 * Draws directly into an inline <svg> element. No external requests, no CDN.
 *
 * EAN-13 is the barcode symbology used at retail POS counters worldwide (UPC-A, used across
 * North America, is just an EAN-13 with a leading "0"), so a value rendered here is scannable by
 * any standard POS/handheld scanner without special configuration - no CompanyCode/CODE128
 * setup required on the scanner's end.
 *
 * Usage:
 *   DinvBarcode.render('mySvgId', '2000000001231', { width: 2, height: 50, displayValue: true, fontSize: 14, margin: 4 });
 *
 * Throws an Error if the value isn't exactly 13 digits with a valid EAN-13 check digit, so callers
 * can fall back to a plain-text label instead of drawing a barcode that would fail to scan.
 */
(function (global) {
    'use strict';

    // Left-hand "odd parity" (L-code) patterns, one 7-module string per digit 0-9.
    var L_CODE = [
        '0001101', '0011001', '0010011', '0111101', '0100011',
        '0110001', '0101111', '0111011', '0110111', '0001011'
    ];

    // Left-hand "even parity" (G-code) patterns - used for whichever left-hand digits the first
    // digit's parity table calls for.
    var G_CODE = [
        '0100111', '0110011', '0011011', '0100001', '0011101',
        '0111001', '0000101', '0010001', '0001001', '0010111'
    ];

    // Right-hand (R-code) patterns - always used for the last 6 digits.
    var R_CODE = [
        '1110010', '1100110', '1101100', '1000010', '1011100',
        '1001110', '1010000', '1000100', '1001000', '1110100'
    ];

    // For each possible first digit (0-9), which pattern (L=0, G=1) each of the next 6 digits uses.
    var PARITY = [
        '000000', '001011', '001101', '001110', '010011',
        '011001', '011100', '010101', '010110', '011010'
    ];

    var START_GUARD = '101';
    var CENTER_GUARD = '01010';
    var END_GUARD = '101';

    function computeCheckDigit(twelveDigits) {
        var sum = 0;
        for (var i = 0; i < 12; i++) {
            var digit = twelveDigits.charCodeAt(i) - 48;
            sum += digit * (i % 2 === 0 ? 1 : 3);
        }
        return (10 - (sum % 10)) % 10;
    }

    function isValid(text) {
        return /^\d{13}$/.test(text) && computeCheckDigit(text.slice(0, 12)) === (text.charCodeAt(12) - 48);
    }

    // Builds the full module string (one char per module, '1' = bar, '0' = space) plus metadata
    // about which modules are "guard" bars (start/center/end), since those print taller than the
    // data bars on a real EAN-13 label.
    function encode(text) {
        if (!isValid(text)) {
            throw new Error('"' + text + '" is not a valid EAN-13 barcode (must be exactly 13 digits with a correct check digit).');
        }

        var firstDigit = text.charCodeAt(0) - 48;
        var parity = PARITY[firstDigit];

        var modules = '';
        var guardMask = '';

        function append(pattern, isGuard) {
            modules += pattern;
            for (var k = 0; k < pattern.length; k++) guardMask += isGuard ? '1' : '0';
        }

        append(START_GUARD, true);
        for (var i = 1; i <= 6; i++) {
            var digit = text.charCodeAt(i) - 48;
            var table = parity.charAt(i - 1) === '1' ? G_CODE : L_CODE;
            append(table[digit], false);
        }
        append(CENTER_GUARD, true);
        for (var j = 7; j <= 12; j++) {
            var d = text.charCodeAt(j) - 48;
            append(R_CODE[d], false);
        }
        append(END_GUARD, true);

        return {
            modules: modules,
            guardMask: guardMask,
            firstDigit: text[0],
            leftDigits: text.slice(1, 7),
            rightDigits: text.slice(7, 13)
        };
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

        var value = typeof text === 'string' ? text.trim() : '';
        var encoded = encode(value); // throws on invalid input - let caller catch it

        // Guard bars (start/center/end) run a bit taller than the data bars - the classic EAN-13
        // look, and it's what makes the guard bars easy for a scanner's decode algorithm to lock
        // onto first.
        var guardExtra = Math.max(4, Math.round(barHeight * 0.12));
        var guardHeight = barHeight + guardExtra;
        var textHeight = displayValue ? fontSize + 6 : 0;

        // Leave room to the left for the first digit, printed outside the bars (standard EAN-13 style).
        var leftQuiet = displayValue ? fontSize * 1.1 : 0;
        var barsWidth = encoded.modules.length * moduleWidth;
        var svgWidth = leftQuiet + barsWidth + margin * 2;
        var svgHeight = guardHeight + textHeight + margin;

        while (svg.firstChild) svg.removeChild(svg.firstChild);
        svg.setAttribute('width', svgWidth);
        svg.setAttribute('height', svgHeight);
        svg.setAttribute('viewBox', '0 0 ' + svgWidth + ' ' + svgHeight);

        var barsStartX = margin + leftQuiet;
        var x = barsStartX;
        for (var i = 0; i < encoded.modules.length; i++) {
            var widthPx = moduleWidth;
            if (encoded.modules[i] === '1') {
                var isGuard = encoded.guardMask[i] === '1';
                var rect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
                rect.setAttribute('x', x);
                rect.setAttribute('y', 0);
                rect.setAttribute('width', widthPx);
                rect.setAttribute('height', isGuard ? guardHeight : barHeight);
                rect.setAttribute('fill', '#000');
                svg.appendChild(rect);
            }
            x += widthPx;
        }

        if (displayValue) {
            var textY = barHeight + fontSize - 2;

            // First digit, hanging to the left of the start guard.
            var firstDigitEl = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            firstDigitEl.setAttribute('x', margin + leftQuiet * 0.15);
            firstDigitEl.setAttribute('y', textY);
            firstDigitEl.setAttribute('text-anchor', 'start');
            firstDigitEl.setAttribute('font-family', 'monospace');
            firstDigitEl.setAttribute('font-size', fontSize);
            firstDigitEl.textContent = encoded.firstDigit;
            svg.appendChild(firstDigitEl);

            // Left group of 6 digits, centered under the left half of the bars.
            var leftGroupWidth = 3 * moduleWidth + 6 * 7 * moduleWidth; // start guard + 6 digits
            var leftTextEl = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            leftTextEl.setAttribute('x', barsStartX + leftGroupWidth / 2);
            leftTextEl.setAttribute('y', textY);
            leftTextEl.setAttribute('text-anchor', 'middle');
            leftTextEl.setAttribute('font-family', 'monospace');
            leftTextEl.setAttribute('font-size', fontSize);
            leftTextEl.setAttribute('letter-spacing', moduleWidth);
            leftTextEl.textContent = encoded.leftDigits;
            svg.appendChild(leftTextEl);

            // Right group of 6 digits, centered under the right half of the bars.
            var rightGroupStartX = barsStartX + leftGroupWidth + 5 * moduleWidth; // + center guard
            var rightGroupWidth = 6 * 7 * moduleWidth + 3 * moduleWidth; // 6 digits + end guard
            var rightTextEl = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            rightTextEl.setAttribute('x', rightGroupStartX + rightGroupWidth / 2);
            rightTextEl.setAttribute('y', textY);
            rightTextEl.setAttribute('text-anchor', 'middle');
            rightTextEl.setAttribute('font-family', 'monospace');
            rightTextEl.setAttribute('font-size', fontSize);
            rightTextEl.setAttribute('letter-spacing', moduleWidth);
            rightTextEl.textContent = encoded.rightDigits;
            svg.appendChild(rightTextEl);
        }
    }

    global.DinvBarcode = { render: render, isValid: isValid, computeCheckDigit: computeCheckDigit };
})(window);
