(function () {
    const EXCELJS_CDNS = [
        "https://cdn.jsdelivr.net/npm/exceljs@4.4.0/dist/exceljs.min.js",
        "https://unpkg.com/exceljs@4.4.0/dist/exceljs.min.js"
    ];
    const INVALID_FILE_CHARS = /[\\/:*?"<>|]/g;
    const INVALID_SHEET_CHARS = /[\\/?*:[\]]/g;
    const DEFAULT_FILE_NAME = "דוח";
    const BLOCK_NO_DATA_MESSAGE = "אין נתונים לייצוא";
    const DASHBOARD_NO_DATA_MESSAGE = "אין נתונים זמינים לייצוא";
    const IMAGE_FAILURE_NOTE = "התרשים לא צורף, אך הנתונים יוצאו בהצלחה";
    const CHART_IMAGE_WIDTH = 620;
    const CHART_IMAGE_HEIGHT = 320;

    let excelJsLoadPromise = null;

    function loadExcelJs() {
        if (window.ExcelJS) return Promise.resolve(window.ExcelJS);
        if (excelJsLoadPromise) return excelJsLoadPromise;

        excelJsLoadPromise = loadExcelJsFromCdn(0);
        return excelJsLoadPromise;
    }

    function loadExcelJsFromCdn(index) {
        const url = EXCELJS_CDNS[index];
        if (!url) {
            excelJsLoadPromise = null;
            return Promise.reject(new Error("Failed to load ExcelJS library from all configured CDNs"));
        }

        console.info("[DashboardExcelExport] Loading ExcelJS library", { url });
        return new Promise((resolve, reject) => {
            const script = document.createElement("script");
            script.src = url;
            script.async = true;
            script.onload = () => {
                if (window.ExcelJS) {
                    console.info("[DashboardExcelExport] ExcelJS library loaded", { url });
                    resolve(window.ExcelJS);
                    return;
                }
                script.remove();
                reject(new Error(`ExcelJS global missing after loading ${url}`));
            };
            script.onerror = () => {
                script.remove();
                reject(new Error(`Network error loading ExcelJS library: ${url}`));
            };
            document.head.appendChild(script);
        }).catch(error => {
            console.error("[DashboardExcelExport] ExcelJS CDN load failed", { url, error });
            return loadExcelJsFromCdn(index + 1);
        });
    }

    function sanitizeFileName(name) {
        const clean = String(name || DEFAULT_FILE_NAME)
            .replace(INVALID_FILE_CHARS, " ")
            .replace(/[\u0000-\u001f]/g, " ")
            .replace(/\s+/g, " ")
            .trim()
            .replace(/[. ]+$/g, "");
        return clean || DEFAULT_FILE_NAME;
    }

    function sanitizeSheetName(name, usedNames) {
        const used = usedNames || new Set();
        const base = String(name || "גיליון")
            .replace(INVALID_SHEET_CHARS, " ")
            .replace(/[\u0000-\u001f]/g, " ")
            .replace(/\s+/g, " ")
            .trim() || "גיליון";

        let candidate = base.slice(0, 31);
        let index = 2;
        while (used.has(candidate)) {
            const suffix = ` ${index}`;
            candidate = base.slice(0, 31 - suffix.length) + suffix;
            index++;
        }
        used.add(candidate);
        return candidate;
    }

    function hasValue(value) {
        return value !== null && value !== undefined && value !== "";
    }

    function normalizeValue(value) {
        if (value === null || value === undefined) return "";
        if (value instanceof Date) return value;
        if (typeof value === "object") return JSON.stringify(value);
        return value;
    }

    function firstArray() {
        for (let i = 0; i < arguments.length; i++) {
            if (Array.isArray(arguments[i])) return arguments[i];
        }
        return [];
    }

    function normalizeBlock(block) {
        const data = block?.data || block?.Data || block?.result || block?.Result || block || {};
        const chartData = block?.chartData || block?.ChartData || data?.chartData || data?.ChartData || {};
        const datasets = firstArray(chartData.datasets, chartData.Datasets, data.datasets, data.Datasets, block?.datasets, block?.Datasets);
        const firstDataset = datasets[0] || {};

        return {
            id: block?.id || block?.ID || block?.chartID || block?.ChartID,
            title: block?.title || block?.Title || block?.chartTitle || block?.ChartTitle || "תצוגה",
            type: String(block?.type || block?.Type || block?.chartType || block?.ChartType || data?.visualizationType || data?.VisualizationType || "bar").toLowerCase(),
            rows: firstArray(block?.rows, block?.Rows, data.rows, data.Rows, data.tableRows, data.TableRows),
            labels: firstArray(block?.labels, block?.Labels, data.labels, data.Labels, chartData.labels, chartData.Labels),
            values: firstArray(block?.values, block?.Values, data.values, data.Values, firstDataset.data, firstDataset.Data),
            imageDataUrl: block?.imageDataUrl || block?.ImageDataUrl || data.imageDataUrl || data.ImageDataUrl || ""
        };
    }

    function tableToAoa(block) {
        const normalized = normalizeBlock(block);
        const rows = normalized.rows;
        if (!rows.length) return null;

        if (Array.isArray(rows[0])) {
            const hasArrayData = rows.some(row => row.some(hasValue));
            return hasArrayData ? rows.map(row => row.map(normalizeValue)) : null;
        }

        const headers = Object.keys(rows[0] || {});
        if (!headers.length) return null;

        const body = rows.map(row => headers.map(header => normalizeValue(row?.[header])));
        const hasData = body.some(row => row.some(hasValue));
        return hasData ? [headers, ...body] : null;
    }

    function chartToAoa(block) {
        const normalized = normalizeBlock(block);
        const labels = normalized.labels;
        const values = normalized.values;
        const length = Math.max(labels.length, values.length);
        if (!length) return null;

        const rows = [];
        for (let i = 0; i < length; i++) {
            rows.push([normalizeValue(labels[i]), normalizeValue(values[i])]);
        }

        const hasData = rows.some(row => row.some(hasValue));
        return hasData ? [["קטגוריה", "ערך"], ...rows] : null;
    }

    function blockToAoa(block) {
        const normalized = normalizeBlock(block);
        if (normalized.type === "table") return tableToAoa(normalized);
        return chartToAoa(block);
    }

    function isChartBlock(block) {
        return normalizeBlock(block).type !== "table";
    }

    function findChartElement(block) {
        const normalized = normalizeBlock(block);
        const id = normalized.id;
        const selectors = [];

        if (block?.canvasId) selectors.push(`#${cssEscape(block.canvasId)}`);
        if (block?.elementId) selectors.push(`#${cssEscape(block.elementId)}`);
        if (id !== null && id !== undefined && id !== "") {
            selectors.push(`#canvas_${cssEscape(id)}`);
            selectors.push(`#chartCard_${cssEscape(id)} canvas`);
            selectors.push(`#chartCard_${cssEscape(id)} svg`);
        }

        for (const selector of selectors) {
            const element = document.querySelector(selector);
            if (element) return element;
        }
        return null;
    }

    function cssEscape(value) {
        if (window.CSS && typeof window.CSS.escape === "function") return window.CSS.escape(String(value));
        return String(value).replace(/[^a-zA-Z0-9_-]/g, "\\$&");
    }

    async function captureChartImage(block) {
        const normalized = normalizeBlock(block);
        if (!isChartBlock(normalized)) return null;
        if (normalized.imageDataUrl) return normalized.imageDataUrl;

        const element = findChartElement(normalized);
        if (!element) throw new Error(`Chart element not found for block ${normalized.id || normalized.title}`);

        if (element instanceof HTMLCanvasElement) {
            if (!element.width || !element.height) throw new Error("Chart canvas has no drawable size");
            return element.toDataURL("image/png");
        }

        if (element instanceof SVGElement) {
            return svgToPngDataUrl(element);
        }

        throw new Error(`Unsupported chart element type: ${element.tagName}`);
    }

    function svgToPngDataUrl(svgElement) {
        return new Promise((resolve, reject) => {
            try {
                const serializer = new XMLSerializer();
                const svgText = serializer.serializeToString(svgElement);
                const svgBlob = new Blob([svgText], { type: "image/svg+xml;charset=utf-8" });
                const url = URL.createObjectURL(svgBlob);
                const image = new Image();
                const rect = svgElement.getBoundingClientRect();
                const width = Math.max(Math.round(rect.width || Number(svgElement.getAttribute("width")) || CHART_IMAGE_WIDTH), 1);
                const height = Math.max(Math.round(rect.height || Number(svgElement.getAttribute("height")) || CHART_IMAGE_HEIGHT), 1);

                image.onload = () => {
                    const canvas = document.createElement("canvas");
                    canvas.width = width;
                    canvas.height = height;
                    const ctx = canvas.getContext("2d");
                    ctx.fillStyle = "#ffffff";
                    ctx.fillRect(0, 0, width, height);
                    ctx.drawImage(image, 0, 0, width, height);
                    URL.revokeObjectURL(url);
                    resolve(canvas.toDataURL("image/png"));
                };
                image.onerror = () => {
                    URL.revokeObjectURL(url);
                    reject(new Error("Failed to render SVG chart as PNG"));
                };
                image.src = url;
            } catch (error) {
                reject(error);
            }
        });
    }

    function addAoaToWorksheet(worksheet, aoa, startRow) {
        aoa.forEach((row, index) => {
            const excelRow = worksheet.getRow(startRow + index);
            row.forEach((value, columnIndex) => {
                const cell = excelRow.getCell(columnIndex + 1);
                cell.value = normalizeValue(value);
                cell.alignment = { horizontal: "right", vertical: "middle", readingOrder: "rtl" };
                if (index === 0) {
                    cell.font = { bold: true };
                    cell.fill = { type: "pattern", pattern: "solid", fgColor: { argb: "FFF8FAFC" } };
                }
            });
            excelRow.commit?.();
        });
    }

    function getAoaColumnCount(aoa) {
        return Math.max(1, ...(aoa || [[]]).map(row => row.length || 0));
    }

    function setWorksheetColumns(worksheet, dataColumnCount, imageStartColumn) {
        const columnCount = Math.max(dataColumnCount, imageStartColumn + 8);
        worksheet.columns = Array.from({ length: columnCount }).map((_, index) => ({
            width: index < dataColumnCount ? 24 : 12
        }));
    }

    async function addBlockWorksheet(workbook, block, usedSheetNames) {
        const normalized = normalizeBlock(block);
        const aoa = blockToAoa(normalized);
        const worksheet = workbook.addWorksheet(sanitizeSheetName(normalized.title, usedSheetNames), {
            views: [{ rightToLeft: true }]
        });
        const dataColumnCount = getAoaColumnCount(aoa || [[BLOCK_NO_DATA_MESSAGE]]);
        const imageStartColumn = dataColumnCount + 2;
        setWorksheetColumns(worksheet, dataColumnCount, imageStartColumn);

        let imageAttached = false;
        let imageError = null;

        addAoaToWorksheet(worksheet, aoa || [[BLOCK_NO_DATA_MESSAGE]], 1);

        if (isChartBlock(normalized) && aoa) {
            try {
                const imageDataUrl = await captureChartImage(normalized);
                if (imageDataUrl) {
                    const imageId = workbook.addImage({ base64: imageDataUrl, extension: "png" });
                    worksheet.addImage(imageId, {
                        tl: { col: imageStartColumn, row: 0 },
                        ext: { width: CHART_IMAGE_WIDTH, height: CHART_IMAGE_HEIGHT }
                    });
                    imageAttached = true;
                }
            } catch (error) {
                imageError = error;
                console.error("[DashboardExcelExport] Chart image capture failed", { block: summarizeBlock(normalized), error });
                const noteCell = worksheet.getCell(1, imageStartColumn + 1);
                noteCell.value = IMAGE_FAILURE_NOTE;
                noteCell.font = { bold: true, color: { argb: "FF9A3412" } };
                noteCell.alignment = { horizontal: "right", readingOrder: "rtl", wrapText: true };
            }
        }

        return {
            hasData: !!aoa,
            imageAttached,
            imageError
        };
    }

    async function downloadWorkbook(workbook, fileName) {
        const safeFileName = `${sanitizeFileName(fileName)}.xlsx`;
        const bytes = await workbook.xlsx.writeBuffer();
        const blob = new Blob([bytes], { type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" });
        const url = URL.createObjectURL(blob);
        const link = document.createElement("a");

        link.href = url;
        link.download = safeFileName;
        link.style.display = "none";
        document.body.appendChild(link);
        link.click();
        link.remove();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
        console.info("[DashboardExcelExport] Workbook download triggered", { fileName: safeFileName, bytes: blob.size });
    }

    function summarizeBlock(block) {
        const normalized = normalizeBlock(block);
        return {
            id: normalized.id,
            title: normalized.title,
            type: normalized.type,
            rowCount: normalized.rows.length,
            labelCount: normalized.labels.length,
            valueCount: normalized.values.length
        };
    }

    async function exportBlock(block) {
        console.info("[DashboardExcelExport] Per-block export requested", summarizeBlock(block));
        const aoa = blockToAoa(block);
        if (!aoa) return { ok: false, message: BLOCK_NO_DATA_MESSAGE };

        const ExcelJS = await loadExcelJs();
        const workbook = new ExcelJS.Workbook();
        workbook.creator = "BlueVision";
        const result = await addBlockWorksheet(workbook, block, new Set());
        await downloadWorkbook(workbook, normalizeBlock(block).title || DEFAULT_FILE_NAME);
        return { ok: true, rowCount: aoa.length - 1, imageAttached: result.imageAttached };
    }

    async function exportDashboard(blocks, dashboardName) {
        const safeBlocks = Array.isArray(blocks) ? blocks : [];
        console.info("[DashboardExcelExport] Full dashboard export requested", {
            dashboardName,
            blockCount: safeBlocks.length,
            blocks: safeBlocks.map(summarizeBlock)
        });

        const preparedBlocks = safeBlocks.map(block => ({ block, aoa: blockToAoa(block) }));
        const exportedCount = preparedBlocks.filter(block => block.aoa).length;

        if (!safeBlocks.length || exportedCount === 0) {
            return { ok: false, message: DASHBOARD_NO_DATA_MESSAGE };
        }

        const ExcelJS = await loadExcelJs();
        const workbook = new ExcelJS.Workbook();
        workbook.creator = "BlueVision";
        const usedSheetNames = new Set();
        let attachedImages = 0;

        for (const item of preparedBlocks) {
            const result = await addBlockWorksheet(workbook, item.block, usedSheetNames);
            if (result.imageAttached) attachedImages++;
        }

        await downloadWorkbook(workbook, dashboardName || DEFAULT_FILE_NAME);
        return { ok: true, exportedCount, sheetCount: workbook.worksheets.length, attachedImages };
    }

    window.DashboardExcelExport = {
        exportBlock,
        exportDashboard,
        sanitizeFileName,
        sanitizeSheetName,
        summarizeBlock,
        getNoDataMessage: () => BLOCK_NO_DATA_MESSAGE
    };
})();
