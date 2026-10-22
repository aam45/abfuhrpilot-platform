import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_BASE } from '../config';
import {
  Container, Destination, Driver, DriverDashboard, InAppNavigation, NavigationLinks, OperationsDashboard, Order,
  Paged, ProblemPhotoDto, ProblemType, RouteCalculationResult, RouteDto, RouteOptimizationResult,
  Stop, User, Vehicle, WasteType,
} from '../models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  constructor(private http: HttpClient) {}

  private list<T>(path: string, params?: Record<string, any>): Observable<Paged<T>> {
    let p = new HttpParams();
    for (const [k, v] of Object.entries(params ?? {})) if (v != null && v !== '') p = p.set(k, v);
    return this.http.get<Paged<T>>(`${API_BASE}/${path}`, { params: p });
  }

  // Catalogue — waste types
  wasteTypes = (q?: any) => this.list<WasteType>('waste-types', q);
  createWasteType = (b: any) => this.http.post<WasteType>(`${API_BASE}/waste-types`, b);
  updateWasteType = (id: string, b: any) => this.http.put<WasteType>(`${API_BASE}/waste-types/${id}`, b);
  deleteWasteType = (id: string) => this.http.delete<void>(`${API_BASE}/waste-types/${id}`);

  // Catalogue — vehicles
  vehicles = (q?: any) => this.list<Vehicle>('vehicles', q);
  createVehicle = (b: any) => this.http.post<Vehicle>(`${API_BASE}/vehicles`, b);
  updateVehicle = (id: string, b: any) => this.http.put<Vehicle>(`${API_BASE}/vehicles/${id}`, b);
  deleteVehicle = (id: string) => this.http.delete<void>(`${API_BASE}/vehicles/${id}`);

  // Catalogue — drivers
  drivers = (q?: any) => this.list<Driver>('drivers', q);
  createDriver = (b: any) => this.http.post<Driver>(`${API_BASE}/drivers`, b);
  updateDriver = (id: string, b: any) => this.http.put<Driver>(`${API_BASE}/drivers/${id}`, b);
  deleteDriver = (id: string) => this.http.delete<void>(`${API_BASE}/drivers/${id}`);

  // Catalogue — destinations
  destinations = (q?: any) => this.list<Destination>('destinations', q);
  createDestination = (b: any) => this.http.post<Destination>(`${API_BASE}/destinations`, b);
  updateDestination = (id: string, b: any) => this.http.put<Destination>(`${API_BASE}/destinations/${id}`, b);
  deleteDestination = (id: string) => this.http.delete<void>(`${API_BASE}/destinations/${id}`);

  // Catalogue — containers
  containers = (q?: any) => this.list<Container>('containers', q);
  createContainer = (b: any) => this.http.post<Container>(`${API_BASE}/containers`, b);
  updateContainer = (id: string, b: any) => this.http.put<Container>(`${API_BASE}/containers/${id}`, b);
  deleteContainer = (id: string) => this.http.delete<void>(`${API_BASE}/containers/${id}`);

  // Routes & stops
  routes = (q?: any) => this.list<RouteDto>('routes', q);
  route = (id: string) => this.http.get<RouteDto>(`${API_BASE}/routes/${id}`);
  createRoute = (b: any) => this.http.post<RouteDto>(`${API_BASE}/routes`, b);
  assignRoute = (id: string, b: any) => this.http.post<RouteDto>(`${API_BASE}/routes/${id}/assign`, b);
  startRoute = (id: string) => this.http.post<RouteDto>(`${API_BASE}/routes/${id}/start`, {});
  completeRoute = (id: string) => this.http.post<RouteDto>(`${API_BASE}/routes/${id}/complete`, {});
  routeStops = (id: string) => this.http.get<Stop[]>(`${API_BASE}/routes/${id}/stops`);
  addStop = (id: string, b: any) => this.http.post<Stop>(`${API_BASE}/routes/${id}/stops`, b);
  reorderStops = (id: string, stopIds: string[]) => this.http.post<Stop[]>(`${API_BASE}/routes/${id}/stops/reorder`, { stopIds });

  // Routing / navigation
  calculateRoute = (id: string) => this.http.post<RouteCalculationResult>(`${API_BASE}/routes/${id}/calculate`, {});
  optimizeRoute = (id: string) => this.http.post<RouteOptimizationResult>(`${API_BASE}/routes/${id}/optimize`, {});
  applyOptimizedOrder = (id: string, stopIds: string[]) => this.http.post<Stop[]>(`${API_BASE}/routes/${id}/apply-optimized-order`, { stopIds });
  navigationLinks = (id: string) => this.http.get<NavigationLinks>(`${API_BASE}/routes/${id}/navigation-links`);
  inAppNavigation = (id: string) => this.http.get<InAppNavigation>(`${API_BASE}/routes/${id}/in-app-navigation`);

  startStop = (id: string) => this.http.post<Stop>(`${API_BASE}/stops/${id}/start`, {});
  completeStop = (id: string) => this.http.post<Stop>(`${API_BASE}/stops/${id}/complete`, {});
  skipStop = (id: string) => this.http.post<Stop>(`${API_BASE}/stops/${id}/skip`, {});
  reportProblem = (id: string, problemType: ProblemType, description?: string, photoIds?: string[]) =>
    this.http.post<Stop>(`${API_BASE}/stops/${id}/report-problem`, { problemType, description, photoIds });

  uploadProblemPhoto = (file: File) => {
    const fd = new FormData();
    fd.append('file', file, file.name);
    return this.http.post<ProblemPhotoDto>(`${API_BASE}/problem-photos`, fd);
  };

  // Orders
  orders = (q?: any) => this.list<Order>('orders', q);
  createOrder = (b: any) => this.http.post<Order>(`${API_BASE}/orders`, b);
  updateOrder = (id: string, b: any) => this.http.put<Order>(`${API_BASE}/orders/${id}`, b);
  scheduleOrder = (id: string, scheduledDate: string) => this.http.post<Order>(`${API_BASE}/orders/${id}/schedule`, { scheduledDate });
  assignOrder = (id: string, routeId: string) => this.http.post<Order>(`${API_BASE}/orders/${id}/assign-to-route`, { routeId });

  // Auth self-service (anonymous)
  requestPasswordReset = (email: string) => this.http.post<{ message: string; devToken?: string }>(`${API_BASE}/auth/request-password-reset`, { email });
  resetPassword = (token: string, newPassword: string) => this.http.post<void>(`${API_BASE}/auth/reset-password`, { token, newPassword });
  verifyEmail = (token: string) => this.http.post<void>(`${API_BASE}/auth/verify-email`, { token });

  // Users (Admin only)
  users = (q?: any) => this.list<User>('users', q);
  createUser = (b: any) => this.http.post<User>(`${API_BASE}/users`, b);
  updateUser = (id: string, b: any) => this.http.put<User>(`${API_BASE}/users/${id}`, b);
  resetUserPassword = (id: string, newPassword: string) => this.http.post<void>(`${API_BASE}/users/${id}/reset-password`, { newPassword });

  // Dashboards
  operations = () => this.http.get<OperationsDashboard>(`${API_BASE}/dashboard/operations`);
  driverDashboard = () => this.http.get<DriverDashboard>(`${API_BASE}/dashboard/driver`);
}
