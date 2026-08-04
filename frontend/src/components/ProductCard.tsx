import type { Product } from '../types/product';

const PRODUCT_IMAGES = {
  sofa: 'https://images.unsplash.com/photo-1555041469-a586c61ea9bc?auto=format&fit=crop&w=900&q=80',
  mesa: 'https://images.unsplash.com/photo-1604578762246-41134e37f9cc?auto=format&fit=crop&w=900&q=80',
  comedor: 'https://images.unsplash.com/photo-1604578762246-41134e37f9cc?auto=format&fit=crop&w=900&q=80',
  modular: 'https://images.unsplash.com/photo-1493663284031-b7e3aaa4cab7?auto=format&fit=crop&w=900&q=80',
  escritorio: 'https://images.unsplash.com/photo-1518455027359-f3f8164ba6bd?auto=format&fit=crop&w=900&q=80',
  oficina: 'https://images.unsplash.com/photo-1518455027359-f3f8164ba6bd?auto=format&fit=crop&w=900&q=80',
  default: 'https://images.unsplash.com/photo-1618221195710-dd6b41faaea6?auto=format&fit=crop&w=900&q=80'
};

function normalizeText(value: string) {
  return value
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase();
}

function getProductImage(product: Product) {
  const rawImage = product.image ?? '';
  const isPlaceholder = rawImage.includes('via.placeholder.com') || rawImage.includes('1505693416388');
  const description = normalizeText(`${product.name} ${product.category}`);

  if (!isPlaceholder && rawImage.trim()) {
    return rawImage;
  }

  if (description.includes('sofa')) return PRODUCT_IMAGES.sofa;
  if (description.includes('mesa')) return PRODUCT_IMAGES.mesa;
  if (description.includes('comedor')) return PRODUCT_IMAGES.comedor;
  if (description.includes('modular')) return PRODUCT_IMAGES.modular;
  if (description.includes('escritorio')) return PRODUCT_IMAGES.escritorio;
  if (description.includes('oficina')) return PRODUCT_IMAGES.oficina;

  return PRODUCT_IMAGES.default;
}

interface Props {
  product: Product;
  onAddToCart: (product: Product) => void;
}

export function ProductCard({ product, onAddToCart }: Props) {
  const productImage = getProductImage(product);

  return (
    <article className="flex h-full min-h-[520px] flex-col overflow-hidden rounded-2xl bg-white shadow-sm ring-1 ring-stone-200">
      <img
        className="h-60 w-full shrink-0 object-cover"
        src={productImage}
        alt={product.name}
        onError={(event) => {
          event.currentTarget.src = PRODUCT_IMAGES.default;
        }}
      />
      <div className="flex flex-1 flex-col space-y-4 p-5">
        <div className="flex items-start justify-between gap-4">
          <div className="min-w-0">
            <p className="text-xs uppercase tracking-[0.3em] text-brand-500">{product.category}</p>
            <h3 className="text-lg font-semibold">{product.name}</h3>
          </div>
          <span className="shrink-0 text-lg font-bold">${product.price}</span>
        </div>

        <div className="text-sm text-stone-600">
          <p><strong>Colores:</strong> {product.colors?.join(', ') ?? 'N/A'}</p>
          <p><strong>Medidas:</strong> {product.measures?.join(' / ') ?? 'N/A'}</p>
        </div>

        <button
          className="mt-auto w-full rounded-xl bg-brand-700 px-4 py-3 font-medium text-white transition hover:bg-brand-900"
          onClick={() => onAddToCart(product)}
        >
          Agregar al carrito
        </button>
      </div>
    </article>
  );
}
