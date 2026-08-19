export type ApiListResponse<T> = {
  data: T[];
  meta?: {
    page: number;
    limit: number;
    total: number;
    totalPage?: number;
    totalPages: number;
  };
  pagination?: {
    page: number;
    limit: number;
    total: number;
    totalPage: number;
    totalPages?: number;
  };
  message?: string;
  success: boolean;
};
