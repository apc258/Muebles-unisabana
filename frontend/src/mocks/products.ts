export type Category = 'salas' | 'comedores';

export interface Product {
  id: string;
  name: string;
  category: Category;
  price: number;
  image: string;
  colors: string[];
  measures: string[];
}

export const products: Product[] = [
  {
    id: 'sofa-nordico',
    name: 'Sofá Nórdico Oslo',
    category: 'salas',
    price: 2499,
    image: 'https://images.unsplash.com/photo-1555041469-a586c61ea9bc?auto=format&fit=crop&w=900&q=80',
    colors: ['Arena', 'Grafito', 'Oliva'],
    measures: ['2.10m', '2.40m']
  },
  {
    id: 'mesa-luna',
    name: 'Mesa Comedor Luna',
    category: 'comedores',
    price: 1899,
    image: 'https://images.unsplash.com/photo-1604578762246-41134e37f9cc?auto=format&fit=crop&w=900&q=80',
    colors: ['Nogal', 'Roble'],
    measures: ['6 puestos', '8 puestos']
  },
  {
    id: 'modular-neo',
    name: 'Modular Neo',
    category: 'salas',
    price: 3199,
    image: 'https://images.unsplash.com/photo-1493663284031-b7e3aaa4cab7?auto=format&fit=crop&w=900&q=80',
    colors: ['Perla', 'Azul humo'],
    measures: ['3 módulos', '4 módulos']
  }
];
