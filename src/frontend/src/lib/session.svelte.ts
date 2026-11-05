/** Состояние сессии. Вход через учётку Keenetic появится вместе с авторизацией в nginx. */
export const session = $state({ unauthorized: false });
