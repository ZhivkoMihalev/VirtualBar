export const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

export const PASSWORD_REGEX = /^(?=.*[A-Z])(?=.*\d).+$/

export type TFn = (key: string) => string
