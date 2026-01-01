function internalFormat(value: number,
    units: string[],
    base: number,
    options?: {
        unit?: string;   // force output unit
        decimals?: number; // default: 2
    }): string
{
    if (!Number.isFinite(value)) {
        throw new Error("bytes must be a finite number");
    }

    if (value === 0) return `0 ${units[0]}`;

    let unitIndex: number;

    if (options?.unit) {
        unitIndex = units.indexOf(options.unit);
        if (unitIndex === -1) {
            throw new Error(`Invalid unit: ${options.unit}`);
        }
    } else {
        unitIndex = Math.min(
            Math.floor(Math.log(value) / Math.log(base)),
            units.length - 1
        );
    }

    const calcValue = value / Math.pow(base, unitIndex);

    const decimals = unitIndex > 0 ? options?.decimals ?? 1 : 0;

    return `${calcValue.toLocaleString(undefined, { minimumFractionDigits: decimals, maximumFractionDigits: decimals })} ${units[unitIndex]}`;
}

type ByteUnit = "B" | "KB" | "MB" | "GB" | "TB" | "PB";
export const ByteUnits: ByteUnit[] = ["B", "KB", "MB", "GB", "TB", "PB"];
export function formatBytes(
    bytes: number,
    options?: {
        unit?: ByteUnit;   // force output unit
        decimals?: number; // default: 2
    }
): string {
    const BASE = 1024;

    return internalFormat(bytes, ByteUnits, BASE, options);
}

type TimeUnit = "ns" | "µs" | "ms" | "s";
export const TimeUnits: TimeUnit[] = ["ns", "µs", "ms", "s"];
export function formatNs(
    nanoseconds: number,
    options?: {
        unit?: TimeUnit;   // force output unit
        decimals?: number; // default: 2
    }
): string {
    const BASE = 1000;

    return internalFormat(nanoseconds, TimeUnits, BASE, options);
}

export function arraysEqual<T>(a: T[], b: T[]): boolean {
    if (a.length !== b.length) return false;
    return a.every((value, index) => value === b[index]);
}