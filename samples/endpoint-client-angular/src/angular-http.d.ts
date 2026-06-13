declare module "@angular/common/http" {
  export class HttpClient {
    get<T>(url: string): T;
    post<T>(url: string, body?: unknown): T;
    delete<T>(url: string): T;
  }
}
