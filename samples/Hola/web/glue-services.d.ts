// Generado por WolfGlue a partir de las clases con [GlueService]. No editar: se regenera al compilar.
// Uso: /// <reference path="glue-services.d.ts" />  y despues  await glue.services.<servicio>.<metodo>(...)

interface GlueServices {
  /** Lista de tareas en memoria, expuesta a la web con el generador del bridge. */
  tareas: {
    /** Todas las tareas. */
    listar(): Promise<GlueTypes.Tarea[]>;
    /** Anade una tarea y la devuelve. */
    anadir(texto: string): Promise<GlueTypes.Tarea>;
    /** Marca o desmarca una tarea. */
    marcar(id: number, hecha: boolean): Promise<boolean>;
  };
}

declare namespace GlueTypes {
  /** Una tarea pendiente. */
  interface Tarea {
    id: number;
    texto: string;
    hecha: boolean;
  }
}

declare const glue: {
  readonly services: GlueServices;
  call(name: string, args?: unknown): Promise<any>;
  on(name: string, fn: (data: any) => void): () => void;
  off(name: string, fn: (data: any) => void): void;
  emit(name: string, data?: unknown, options?: { to?: string }): void;
  readonly [key: string]: any;
};
