export type Category = 'salas' | 'comedores' | 'recamaras' | 'oficina';

export interface Product {
  id: string;
  name: string;
  category: string;
  price: number;
  image: string;
  colors: string[];
  measures: string[];
}
