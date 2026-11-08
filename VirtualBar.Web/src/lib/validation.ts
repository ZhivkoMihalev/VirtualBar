export const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

export const PASSWORD_REGEX = /^(?=.*[A-Z])(?=.*\d).+$/

/** The part of i18next's `t` a form schema uses: a key, and values for its placeholders. */
export type TFn = (key: string, values?: Record<string, unknown>) => string
