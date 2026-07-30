import { useEffect, useState } from 'react';
import { getEvidencePhotoBlob } from './api';
import type { EvidencePhotoReference } from './pending';

export function EvidencePhoto({
  developmentUserKey,
  photo
}: {
  developmentUserKey: string | undefined;
  photo: EvidencePhotoReference;
}) {
  const [source, setSource] = useState<string | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let active = true;
    let objectUrl: string | null = null;
    void getEvidencePhotoBlob(developmentUserKey, photo)
      .then((blob) => {
        if (!active) return;
        objectUrl = URL.createObjectURL(blob);
        setSource(objectUrl);
        setFailed(false);
      })
      .catch(() => {
        if (active) setFailed(true);
      });
    return () => {
      active = false;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [developmentUserKey, photo]);

  return (
    <figure className="reinspection-evidence-photo">
      {source
        ? <img src={source} alt={photo.altText} />
        : <div>{failed ? '사진을 불러오지 못함' : '사진 불러오는 중'}</div>}
      <figcaption><strong>{photo.altText}</strong><span>{Math.ceil(photo.byteSize / 1024)}KB</span></figcaption>
    </figure>
  );
}
