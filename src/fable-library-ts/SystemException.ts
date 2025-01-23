import { class_type, TypeInfo } from "./Reflection.js";

export class SystemException extends Error {
    constructor() {
        super();
    }
}

export function SystemException_$reflection(): TypeInfo {
    return class_type("System.SystemException", undefined, SystemException, class_type("System.Exception"));
}

export function SystemException_$ctor(): SystemException {
    return new SystemException();
}

export class TimeoutException extends SystemException {
    constructor() {
        super();
    }
}

export function TimeoutException_$reflection(): TypeInfo {
    return class_type("System.TimeoutException", undefined, TimeoutException, SystemException_$reflection());
}

export function TimeoutException_$ctor(): TimeoutException {
    return new TimeoutException();
}

