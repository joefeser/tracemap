import { HttpClient } from "@angular/common/http";
import { environment } from "../environments/environment";

export interface RunnerCheckIn {
  runnerId: string;
}

export class RunnerService {
  constructor(private http: HttpClient) {}

  getById(runnerId: string) {
    return this.http.get(`${environment.apiUri}/admin/runner/get-by-id/${runnerId}?includeHistory=true`);
  }

  checkIn(body: RunnerCheckIn) {
    return this.http.post(`${environment.apiUri}/admin/runner/check-in`, body);
  }

  archive(runnerId: string) {
    return this.http.delete(`${environment.apiUri}/admin/runner/archive/${runnerId}`);
  }

  dynamic(path: string) {
    return this.http.get(path);
  }
}
